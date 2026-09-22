using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Cultos.App;

public sealed record RemoteCommand(string Command, string? Value);
public sealed record RemoteSceneState(string Key, string Name, string Icon, string StateLabel, bool IsLive);
public sealed record RemoteControlState(
    string ChurchName,
    string ServiceName,
    string LiveTitle,
    string? ActiveSceneKey,
    IReadOnlyList<RemoteSceneState> Scenes);
public sealed record RemoteControlStatus(bool IsRunning, string Url, string? Error = null);

public sealed class RemoteControlServer : IAsyncDisposable
{
    private readonly ConcurrentDictionary<Guid, WebSocket> _clients = new();
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);
    private WebApplication? _app;
    private string _pin = "";
    private string _stateJson = "{}";

    public event EventHandler<RemoteCommand>? CommandReceived;

    public bool IsRunning => _app is not null;
    public int Port { get; private set; }
    public string Url => $"http://{ResolveLanAddress()}:{Port}";

    public static string GeneratePin() => RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");

    public async Task<RemoteControlStatus> StartAsync(int port, string pin)
    {
        try
        {
            await StopAsync();

            Port = Math.Clamp(port, 1024, 65535);
            _pin = string.IsNullOrWhiteSpace(pin) ? GeneratePin() : pin.Trim();

            var builder = WebApplication.CreateSlimBuilder();
            builder.Logging.ClearProviders();
            builder.WebHost.UseUrls($"http://0.0.0.0:{Port}");

            var app = builder.Build();
            app.UseWebSockets(new WebSocketOptions
            {
                KeepAliveInterval = TimeSpan.FromSeconds(30)
            });

            app.MapGet("/", () => Results.Content(RemotePage, "text/html; charset=utf-8"));
            app.MapGet("/health", () => Results.Json(new { ok = true }));

            app.Map("/ws", async context =>
            {
                if (!context.WebSockets.IsWebSocketRequest)
                {
                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                    return;
                }

                var suppliedPin = context.Request.Query["pin"].ToString();
                if (!CryptographicOperations.FixedTimeEquals(
                        Encoding.UTF8.GetBytes(suppliedPin),
                        Encoding.UTF8.GetBytes(_pin)))
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return;
                }

                using var socket = await context.WebSockets.AcceptWebSocketAsync();
                var id = Guid.NewGuid();
                _clients[id] = socket;

                try
                {
                    await SendTextAsync(socket, _stateJson, context.RequestAborted);
                    await ReceiveLoopAsync(socket, context.RequestAborted);
                }
                finally
                {
                    _clients.TryRemove(id, out _);
                }
            });

            await app.StartAsync();
            _app = app;
            return new RemoteControlStatus(true, Url);
        }
        catch (Exception ex)
        {
            AppLogger.Error("No se pudo iniciar el control remoto local", ex);
            await StopAsync();
            return new RemoteControlStatus(false, $"http://127.0.0.1:{port}", ex.Message);
        }
    }

    public async Task StopAsync()
    {
        foreach (var client in _clients.Values)
        {
            try
            {
                if (client.State == WebSocketState.Open)
                    await client.CloseAsync(WebSocketCloseStatus.NormalClosure, "Servidor detenido", CancellationToken.None);
            }
            catch
            {
                // Un cliente puede desaparecer sin cerrar correctamente.
            }
        }
        _clients.Clear();

        if (_app is null) return;

        try
        {
            await _app.StopAsync(TimeSpan.FromSeconds(2));
            await _app.DisposeAsync();
        }
        catch (Exception ex)
        {
            AppLogger.Error("No se pudo detener limpiamente el control remoto", ex);
        }
        finally
        {
            _app = null;
        }
    }

    public void UpdateState(RemoteControlState state)
    {
        _stateJson = JsonSerializer.Serialize(new
        {
            type = "state",
            churchName = state.ChurchName,
            serviceName = state.ServiceName,
            liveTitle = state.LiveTitle,
            activeSceneKey = state.ActiveSceneKey,
            scenes = state.Scenes
        }, _json);

        _ = BroadcastAsync(_stateJson);
    }

    private async Task ReceiveLoopAsync(WebSocket socket, CancellationToken cancellationToken)
    {
        var buffer = new byte[4096];

        while (!cancellationToken.IsCancellationRequested && socket.State == WebSocketState.Open)
        {
            WebSocketReceiveResult result;
            using var stream = new MemoryStream();

            do
            {
                result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
                if (result.MessageType == WebSocketMessageType.Close) return;
                stream.Write(buffer, 0, result.Count);
            }
            while (!result.EndOfMessage);

            if (result.MessageType != WebSocketMessageType.Text) continue;

            try
            {
                var json = Encoding.UTF8.GetString(stream.ToArray());
                using var document = JsonDocument.Parse(json);
                var root = document.RootElement;
                var command = root.TryGetProperty("command", out var commandElement)
                    ? commandElement.GetString()
                    : null;
                var value = root.TryGetProperty("value", out var valueElement)
                    ? valueElement.GetString()
                    : null;

                if (!string.IsNullOrWhiteSpace(command))
                    CommandReceived?.Invoke(this, new RemoteCommand(command, value));
            }
            catch (Exception ex)
            {
                AppLogger.Error("Comando remoto no válido", ex);
            }
        }
    }

    private async Task BroadcastAsync(string json)
    {
        if (_clients.IsEmpty) return;

        foreach (var pair in _clients.ToArray())
        {
            var socket = pair.Value;
            if (socket.State != WebSocketState.Open)
            {
                _clients.TryRemove(pair.Key, out _);
                continue;
            }

            try
            {
                await SendTextAsync(socket, json, CancellationToken.None);
            }
            catch
            {
                _clients.TryRemove(pair.Key, out _);
            }
        }
    }

    private static Task SendTextAsync(WebSocket socket, string text, CancellationToken cancellationToken)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        return socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, cancellationToken);
    }

    private static string ResolveLanAddress()
    {
        try
        {
            var candidates = NetworkInterface.GetAllNetworkInterfaces()
                .Where(nic =>
                    nic.OperationalStatus == OperationalStatus.Up &&
                    nic.NetworkInterfaceType is NetworkInterfaceType.Ethernet or NetworkInterfaceType.Wireless80211)
                .Select(nic => new
                {
                    Interface = nic,
                    Properties = nic.GetIPProperties()
                })
                .SelectMany(item => item.Properties.UnicastAddresses
                    .Where(address =>
                        address.Address.AddressFamily == AddressFamily.InterNetwork &&
                        !IPAddress.IsLoopback(address.Address) &&
                        !address.Address.ToString().StartsWith("169.254.", StringComparison.Ordinal))
                    .Select(address => new
                    {
                        address.Address,
                        HasGateway = item.Properties.GatewayAddresses.Any(g =>
                            g.Address.AddressFamily == AddressFamily.InterNetwork &&
                            !g.Address.Equals(IPAddress.Any))
                    }))
                .OrderByDescending(x => x.HasGateway)
                .ThenByDescending(x => IsPrivateAddress(x.Address))
                .Select(x => x.Address)
                .ToList();

            if (candidates.Count > 0)
                return candidates[0].ToString();

            return Dns.GetHostEntry(Dns.GetHostName()).AddressList
                .FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(a))
                ?.ToString() ?? "127.0.0.1";
        }
        catch
        {
            return "127.0.0.1";
        }
    }

    private static bool IsPrivateAddress(IPAddress address)
    {
        var bytes = address.GetAddressBytes();
        return bytes[0] == 10 ||
               (bytes[0] == 172 && bytes[1] is >= 16 and <= 31) ||
               (bytes[0] == 192 && bytes[1] == 168);
    }

    public async ValueTask DisposeAsync() => await StopAsync();

    private const string RemotePage = """
<!doctype html>
<html lang="es">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1,viewport-fit=cover">
<title>Cultos · Control remoto</title>
<style>
:root{color-scheme:dark;--bg:#10110f;--panel:#191a17;--raised:#24251f;--border:#34352e;--text:#f3f0e7;--muted:#aaa89e;--accent:#c9ac70;--live:#d36a61}
*{box-sizing:border-box}body{margin:0;background:var(--bg);color:var(--text);font-family:system-ui,-apple-system,Segoe UI,sans-serif}
header{position:sticky;top:0;background:#121310eF;border-bottom:1px solid var(--border);padding:14px 16px;backdrop-filter:blur(10px);z-index:2}
h1{font-size:18px;margin:0}.sub{color:var(--muted);font-size:12px;margin-top:3px}
main{padding:14px;max-width:900px;margin:auto}.card{background:var(--panel);border:1px solid var(--border);border-radius:14px;padding:14px;margin-bottom:14px}
.login{display:grid;grid-template-columns:1fr auto;gap:8px}input,button{font:inherit;border-radius:10px;border:1px solid var(--border)}
input{background:#0d0e0c;color:var(--text);padding:13px}button{background:var(--raised);color:var(--text);padding:13px 14px;font-weight:650}
button:active{transform:scale(.98)}button.live{border-color:var(--live);background:#47231f}.status{font-size:12px;color:var(--muted);margin-top:9px}
.grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(118px,1fr));gap:9px}.scene{min-height:76px}.scene span{display:block;color:var(--muted);font-size:10px;margin-top:5px;font-weight:400}
.controls{display:grid;grid-template-columns:repeat(4,1fr);gap:8px}.danger{border-color:#764038}.primary{border-color:#6d5b37;background:#30291d}
.now{font-size:13px;color:var(--muted)}.now strong{color:var(--text);display:block;font-size:17px;margin-top:4px}
.hidden{display:none}@media(max-width:520px){.controls{grid-template-columns:repeat(2,1fr)}}
</style>
</head>
<body>
<header><h1 id="church">Cultos</h1><div class="sub" id="service">Control remoto local</div></header>
<main>
<section class="card" id="loginCard">
  <div class="login"><input id="pin" inputmode="numeric" maxlength="6" placeholder="PIN de 6 dígitos"><button onclick="connect()">Conectar</button></div>
  <div class="status" id="status">Conéctate al mismo Wi‑Fi o red local que la computadora de Cultos.</div>
</section>
<section id="remote" class="hidden">
  <div class="card now">EN VIVO<strong id="liveTitle">Sin contenido</strong></div>
  <div class="card"><div class="grid" id="scenes"></div></div>
  <div class="card controls">
    <button class="primary" onclick="send('send')">Enviar preview</button>
    <button onclick="send('previous')">← Anterior</button>
    <button onclick="send('next')">Siguiente →</button>
    <button onclick="send('play')">▶ Reproducir</button>
    <button onclick="send('pause')">Ⅱ Pausa</button>
    <button onclick="send('logo')">◇ Logo</button>
    <button class="danger" onclick="send('black')">■ Negro</button>
    <button class="danger" onclick="send('clear')">Limpiar</button>
  </div>
</section>
</main>
<script>
let ws;
const pinBox=document.getElementById('pin');
pinBox.value=localStorage.getItem('cultos-pin')||'';
function connect(){
  const pin=pinBox.value.trim();
  if(!pin){document.getElementById('status').textContent='Escribe el PIN que aparece en Cultos.';return}
  localStorage.setItem('cultos-pin',pin);
  const scheme=location.protocol==='https:'?'wss':'ws';
  ws=new WebSocket(scheme+'://'+location.host+'/ws?pin='+encodeURIComponent(pin));
  document.getElementById('status').textContent='Conectando…';
  ws.onopen=()=>{document.getElementById('status').textContent='Conectado';document.getElementById('remote').classList.remove('hidden')};
  ws.onclose=()=>{document.getElementById('status').textContent='Desconectado. Revisa el PIN o la red.';document.getElementById('remote').classList.add('hidden')};
  ws.onerror=()=>document.getElementById('status').textContent='No se pudo conectar.';
  ws.onmessage=e=>{try{const m=JSON.parse(e.data);if(m.type==='state')render(m)}catch{}};
}
function send(command,value){if(ws&&ws.readyState===1)ws.send(JSON.stringify({command,value}))}
function render(s){
  document.getElementById('church').textContent=s.churchName||'Cultos';
  document.getElementById('service').textContent=s.serviceName||'';
  document.getElementById('liveTitle').textContent=s.liveTitle||'Sin contenido';
  const host=document.getElementById('scenes');host.innerHTML='';
  (s.scenes||[]).forEach(sc=>{
    const b=document.createElement('button');b.className='scene'+(sc.isLive?' live':'');
    b.innerHTML=(sc.icon||'◆')+' '+escapeHtml(sc.name)+'<span>'+escapeHtml(sc.stateLabel||'')+'</span>';
    b.onclick=()=>send('scene',sc.key);host.appendChild(b);
  });
}
function escapeHtml(v){const d=document.createElement('div');d.textContent=v||'';return d.innerHTML}
if(pinBox.value)connect();
</script>
</body>
</html>
""";
}

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;

namespace LgHdmiTray
{
    internal sealed class LgResult
    {
        public bool Ok;
        public string Error = "";
        public string ClientKey = "";
        public bool PairingFailed;
        public bool Switched;
    }

    internal static class LgTvClient
    {
        private static readonly JavaScriptSerializer Json = new JavaScriptSerializer();
        private static readonly string[] InputIds = { "HDMI_1", "HDMI_2", "HDMI_3", "HDMI_4" };
        private static readonly string[] AppIds =
        {
            "com.webos.app.hdmi1", "com.webos.app.hdmi2",
            "com.webos.app.hdmi3", "com.webos.app.hdmi4"
        };

        public static int Port
        {
            get
            {
                int port;
                string text = Environment.GetEnvironmentVariable("LGHDMI_PORT");
                if (!string.IsNullOrWhiteSpace(text) && int.TryParse(text, out port) &&
                    port > 0 && port < 65536) return port;
                return 3000;
            }
        }

        public static List<string> Discover(int timeoutMs)
        {
            List<string> found = new List<string>();
            try
            {
                using (UdpClient udp = new UdpClient())
                {
                    udp.Client.ReceiveTimeout = 400;
                    string search = "M-SEARCH * HTTP/1.1\r\n" +
                                    "HOST: 239.255.255.250:1900\r\n" +
                                    "MAN: \"ssdp:discover\"\r\n" +
                                    "MX: 2\r\n" +
                                    "ST: urn:lge-com:service:webos-second-screen:1\r\n\r\n";
                    byte[] data = Encoding.ASCII.GetBytes(search);
                    udp.Send(data, data.Length, new IPEndPoint(IPAddress.Parse("239.255.255.250"), 1900));

                    DateTime deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
                    DateTime? first = null;
                    while (DateTime.UtcNow < deadline)
                    {
                        try
                        {
                            IPEndPoint remote = new IPEndPoint(IPAddress.Any, 0);
                            byte[] response = udp.Receive(ref remote);
                            string text = Encoding.ASCII.GetString(response);
                            Match match = Regex.Match(text, "(?im)^LOCATION:\\s*(http://\\S+)");
                            if (match.Success)
                            {
                                Uri uri;
                                if (Uri.TryCreate(match.Groups[1].Value, UriKind.Absolute, out uri) &&
                                    !found.Contains(uri.Host))
                                    found.Add(uri.Host);
                            }
                            if (found.Count > 0 && !first.HasValue) first = DateTime.UtcNow;
                            if (first.HasValue &&
                                (DateTime.UtcNow - first.Value).TotalMilliseconds > 700) break;
                        }
                        catch (SocketException ex)
                        {
                            if (ex.SocketErrorCode != SocketError.TimedOut) break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Write("Error buscando TV: " + ex.Message);
            }
            return found;
        }

        private static ClientWebSocket Connect(string ip, int timeoutMs)
        {
            // Igual que el script de pi3 (lgtv.py), que funciona desde
            // septiembre: una sola conexion ws sin cifrar al puerto 3000
            // (o el de la variable LGHDMI_PORT). Sin wss:3001.
            string host = ip.Contains(":") ? "[" + ip + "]" : ip;
            return ConnectSocket(new Uri("ws://" + host + ":" + Port + "/"), timeoutMs);
        }

        private static ClientWebSocket ConnectSocket(Uri uri, int timeoutMs)
        {
            ClientWebSocket ws = new ClientWebSocket();
            try
            {
                ws.Options.KeepAliveInterval = TimeSpan.FromSeconds(15);
                using (CancellationTokenSource cts = new CancellationTokenSource(timeoutMs))
                {
                    ws.ConnectAsync(uri, cts.Token).GetAwaiter().GetResult();
                }
                return ws;
            }
            catch
            {
                try { ws.Dispose(); } catch { }
                throw;
            }
        }

        private static void SendJson(ClientWebSocket ws, object value)
        {
            string json = Json.Serialize(value);
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true,
                CancellationToken.None).GetAwaiter().GetResult();
        }

        private static void SendText(ClientWebSocket ws, string text)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(text);
            ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true,
                CancellationToken.None).GetAwaiter().GetResult();
        }

        private static Dictionary<string, object> ReceiveJson(ClientWebSocket ws, int timeoutMs)
        {
            byte[] buffer = new byte[65536];
            using (MemoryStream stream = new MemoryStream())
            using (CancellationTokenSource cts = new CancellationTokenSource(Math.Max(100, timeoutMs)))
            {
                try
                {
                    while (true)
                    {
                        WebSocketReceiveResult result = ws.ReceiveAsync(
                            new ArraySegment<byte>(buffer), cts.Token).GetAwaiter().GetResult();
                        if (result.MessageType == WebSocketMessageType.Close) return null;
                        stream.Write(buffer, 0, result.Count);
                        if (result.EndOfMessage) break;
                    }
                }
                catch (OperationCanceledException)
                {
                    return null;
                }

                string text = Encoding.UTF8.GetString(stream.ToArray());
                if (string.IsNullOrWhiteSpace(text)) return null;
                try
                {
                    return Json.Deserialize<Dictionary<string, object>>(text);
                }
                catch (Exception ex)
                {
                    Log.Write("Respuesta no valida de la TV: " + ex.Message);
                    return null;
                }
            }
        }

        private static OrderedDictionary BuildManifest()
        {
            // Manifiesto EXACTO al de pi3 (/home/eph/lgtv.py), el que lleva
            // funcionando desde septiembre de 2026 en esta TV (webOS 3.4.3):
            // sin bloque "signed" ni "signatures", con signedAppId, vendor y
            // los nombres localizados de pi3, y su lista de permisos en el
            // mismo orden. La clave guardada queda ligada al manifiesto con
            // el que se emparejo: tras este cambio hay que emparejar de nuevo
            // para que la TV emita una clave nueva con este alcance.
            OrderedDictionary vendor = new OrderedDictionary();
            vendor["vendorId"] = "com.lge";

            OrderedDictionary appNames = new OrderedDictionary();
            appNames[""] = "LG Remote";
            appNames["en-GB"] = "LG Remote";
            appNames["es-ES"] = "LG Remote";

            OrderedDictionary vendorNames = new OrderedDictionary();
            vendorNames[""] = "LG Electronics";

            OrderedDictionary manifest = new OrderedDictionary();
            manifest["manifestVersion"] = 1;
            manifest["appVersion"] = "1.1";
            manifest["signedAppId"] = "com.lge.test";
            manifest["appId"] = "com.lge.test";
            manifest["vendor"] = vendor;
            manifest["localizedAppNames"] = appNames;
            manifest["localizedVendorNames"] = vendorNames;
            manifest["permissions"] = new string[]
            {
                "LAUNCH", "LAUNCH_WEBAPP", "APP_TO_APP", "CLOSE",
                "CONTROL_AUDIO", "CONTROL_DISPLAY", "CONTROL_INPUT_JOYSTICK",
                "CONTROL_INPUT_MEDIA_RECORDING", "CONTROL_INPUT_MEDIA_PLAYBACK",
                "CONTROL_INPUT_TV", "CONTROL_POWER", "READ_APP_STATUS",
                "READ_CURRENT_CHANNEL", "READ_INPUT_DEVICE_LIST", "READ_NETWORK_STATE",
                "READ_RUNNING_APPS", "READ_TV_CHANNEL_LIST",
                "WRITE_NOTIFICATION_TOAST", "READ_POWER_STATE", "READ_COUNTRY_INFO"
            };
            return manifest;
        }

        private static Dictionary<string, object> Payload(string name, object value)
        {
            Dictionary<string, object> payload = new Dictionary<string, object>();
            payload[name] = value;
            return payload;
        }

        private static string GetString(Dictionary<string, object> dictionary, string key)
        {
            object value;
            if (dictionary != null && dictionary.TryGetValue(key, out value) && value != null)
                return Convert.ToString(value);
            return null;
        }

        private static bool Register(ClientWebSocket ws, string clientKey, int timeoutMs,
            out string newKey, out bool pairingFailed, out string error)
        {
            newKey = "";
            pairingFailed = false;
            error = "";

            // Igual que pi3: "client-key" solo se envia si hay una guardada;
            // en un emparejamiento desde cero el campo no va en el payload.
            Dictionary<string, object> payload = new Dictionary<string, object>();
            payload["forcePairing"] = false;
            payload["pairingType"] = "PROMPT";
            payload["manifest"] = BuildManifest();
            if (!string.IsNullOrWhiteSpace(clientKey))
                payload["client-key"] = clientKey;

            Dictionary<string, object> request = new Dictionary<string, object>();
            request["type"] = "register";
            request["id"] = "register_0";
            request["payload"] = payload;
            SendJson(ws, request);

            DateTime deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            while (DateTime.UtcNow < deadline)
            {
                int remaining = (int)(deadline - DateTime.UtcNow).TotalMilliseconds;
                if (remaining < 100) remaining = 100;
                Dictionary<string, object> message = ReceiveJson(ws, remaining);
                if (message == null) break;
                string type = GetString(message, "type");
                if (type == "registered")
                {
                    object payloadObject;
                    if (message.TryGetValue("payload", out payloadObject))
                    {
                        Dictionary<string, object> responsePayload =
                            payloadObject as Dictionary<string, object>;
                        if (responsePayload != null)
                            newKey = GetString(responsePayload, "client-key") ?? "";
                    }
                    return true;
                }
                if (type == "error")
                {
                    pairingFailed = true;
                    error = GetString(message, "error");
                    if (string.IsNullOrWhiteSpace(error))
                        error = "La TV rechazo el emparejamiento.";
                    return false;
                }
            }

            if (string.IsNullOrWhiteSpace(clientKey))
                error = "La TV no confirmo el emparejamiento. Acepta el aviso en la pantalla de la TV.";
            else
                error = "La TV no respondio con la autorizacion guardada.";
            return false;
        }

        private static Dictionary<string, object> SendRequest(ClientWebSocket ws, string uri,
            Dictionary<string, object> payload, int timeoutMs)
        {
            string id = "req_" + Guid.NewGuid().ToString("N").Substring(0, 10);
            Dictionary<string, object> request = new Dictionary<string, object>();
            request["type"] = "request";
            request["id"] = id;
            request["uri"] = uri;
            request["payload"] = payload;
            SendJson(ws, request);

            DateTime deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            while (DateTime.UtcNow < deadline)
            {
                int remaining = (int)(deadline - DateTime.UtcNow).TotalMilliseconds;
                if (remaining < 100) remaining = 100;
                Dictionary<string, object> message = ReceiveJson(ws, remaining);
                if (message == null) break;
                string type = GetString(message, "type");
                if (type == "error")
                {
                    string messageText = GetString(message, "error");
                    throw new Exception(string.IsNullOrWhiteSpace(messageText)
                        ? "La TV devolvio un error." : messageText);
                }
                if (GetString(message, "id") == id) return message;
            }
            throw new TimeoutException("La TV no respondio a la peticion.");
        }

        public static LgResult Pair(string ip, int timeoutSeconds)
        {
            LgResult result = new LgResult();
            ClientWebSocket ws = null;
            try
            {
                ws = Connect(ip, 8000);
                string newKey;
                bool pairingFailed;
                string error;
                int timeout = Math.Min(120, Math.Max(10, timeoutSeconds)) * 1000;
                bool registered = Register(ws, "", timeout, out newKey, out pairingFailed, out error);
                if (!registered)
                {
                    result.PairingFailed = pairingFailed;
                    result.Error = error;
                    return result;
                }
                if (string.IsNullOrWhiteSpace(newKey))
                {
                    result.Error = "La TV se registro pero no devolvio una clave de autorizacion.";
                    return result;
                }
                result.Ok = true;
                result.ClientKey = newKey;
                return result;
            }
            catch (Exception ex)
            {
                result.Error = FriendlyError(ex, ip);
                Log.Write("Error emparejando: " + ex);
                return result;
            }
            finally
            {
                if (ws != null) { try { ws.Dispose(); } catch { } }
            }
        }

        public static LgResult Switch(string ip, string clientKey, int hdmiNumber)
        {
            LgResult result = new LgResult();
            ClientWebSocket ws = null;
            try
            {
                if (hdmiNumber < 1 || hdmiNumber > 4)
                    throw new ArgumentOutOfRangeException("hdmiNumber");
                if (string.IsNullOrWhiteSpace(ip))
                {
                    result.Error = "No hay una TV configurada.";
                    return result;
                }
                if (string.IsNullOrWhiteSpace(clientKey))
                {
                    result.Error = "La TV no esta emparejada. Abre Configurar TV y pulsa Emparejar TV.";
                    return result;
                }

                ws = Connect(ip, 8000);
                string newKey;
                bool pairingFailed;
                string error;
                bool registered = Register(ws, clientKey, 10000,
                    out newKey, out pairingFailed, out error);
                if (!registered)
                {
                    result.PairingFailed = pairingFailed;
                    result.Error = pairingFailed && !string.IsNullOrWhiteSpace(clientKey)
                        ? "La TV no acepto la autorizacion guardada. Abre Configurar TV y empareja de nuevo."
                        : error;
                    return result;
                }
                result.ClientKey = string.IsNullOrWhiteSpace(newKey) ? clientKey : newKey;

                bool switched = false;
                try
                {
                    SendRequest(ws, "ssap://tv/switchInput",
                        Payload("inputId", InputIds[hdmiNumber - 1]), 6000);
                    switched = true;
                }
                catch (Exception ex)
                {
                    Log.Write("switchInput fallo: " + ex.Message);
                }

                if (!switched)
                {
                    SendRequest(ws, "ssap://system.launcher/launch",
                        Payload("id", AppIds[hdmiNumber - 1]), 8000);
                    switched = true;
                }

                result.Ok = switched;
                result.Switched = switched;
                if (!switched) result.Error = "La TV no cambio de entrada.";
                return result;
            }
            catch (Exception ex)
            {
                result.Error = FriendlyError(ex, ip);
                Log.Write("Error cambiando a HDMI " + hdmiNumber + ": " + ex);
                return result;
            }
            finally
            {
                if (ws != null) { try { ws.Dispose(); } catch { } }
            }
        }

        // Orden SSAP sin carga util fija (volumen, apagar, lanzar app...).
        // Mismos URIs que lgtv.py de pi3. No toca el camino de Switch
        // (HDMI), que queda exactamente igual.
        public static LgResult Command(string ip, string clientKey, string uri,
            Dictionary<string, object> payload)
        {
            LgResult result = new LgResult();
            ClientWebSocket ws = null;
            try
            {
                if (string.IsNullOrWhiteSpace(ip))
                {
                    result.Error = "No hay una TV configurada.";
                    return result;
                }
                if (string.IsNullOrWhiteSpace(clientKey))
                {
                    result.Error = "La TV no esta emparejada. Abre Configurar TV y pulsa Emparejar TV.";
                    return result;
                }

                ws = Connect(ip, 8000);
                string newKey;
                bool pairingFailed;
                string error;
                bool registered = Register(ws, clientKey, 10000,
                    out newKey, out pairingFailed, out error);
                if (!registered)
                {
                    result.PairingFailed = pairingFailed;
                    result.Error = pairingFailed && !string.IsNullOrWhiteSpace(clientKey)
                        ? "La TV no acepto la autorizacion guardada. Abre Configurar TV y empareja de nuevo."
                        : error;
                    return result;
                }
                result.ClientKey = string.IsNullOrWhiteSpace(newKey) ? clientKey : newKey;

                SendRequest(ws, uri, payload ?? new Dictionary<string, object>(), 6000);
                result.Ok = true;
                return result;
            }
            catch (Exception ex)
            {
                result.Error = FriendlyError(ex, ip);
                Log.Write("Error en orden " + uri + ": " + ex);
                return result;
            }
            finally
            {
                if (ws != null) { try { ws.Dispose(); } catch { } }
            }
        }

        public static LgResult Home(string ip, string clientKey)
        {
            LgResult result = Command(ip, clientKey, "ssap://system.launcher/launch",
                Payload("id", "com.webos.app.home"));
            if (result.Ok) return result;
            Log.Write("Inicio por launcher fallo (" + result.Error + "); probando boton HOME.");
            return Button(ip, clientKey, "HOME");
        }

        // Botones del mando (OK, flechas, Exit...) por el socket de entrada:
        // se pide ssap://com.webos.service.networkinput/getPointerInputSocket,
        // la TV devuelve socketPath y se abre un segundo WebSocket a esa ruta.
        // El boton va como texto plano "type:button\nname:XXX\n\n", igual
        // que LGWebOSRemote (LGTV/cursor.py). No hay respuesta estructurada:
        // si el envio no falla, se da por enviado.
        public static LgResult Button(string ip, string clientKey, string buttonName)
        {
            LgResult result = new LgResult();
            ClientWebSocket ws = null;
            ClientWebSocket input = null;
            try
            {
                if (string.IsNullOrWhiteSpace(ip))
                {
                    result.Error = "No hay una TV configurada.";
                    return result;
                }
                if (string.IsNullOrWhiteSpace(clientKey))
                {
                    result.Error = "La TV no esta emparejada. Abre Configurar TV y pulsa Emparejar TV.";
                    return result;
                }

                ws = Connect(ip, 8000);
                string newKey;
                bool pairingFailed;
                string error;
                bool registered = Register(ws, clientKey, 10000,
                    out newKey, out pairingFailed, out error);
                if (!registered)
                {
                    result.PairingFailed = pairingFailed;
                    result.Error = pairingFailed && !string.IsNullOrWhiteSpace(clientKey)
                        ? "La TV no acepto la autorizacion guardada. Abre Configurar TV y empareja de nuevo."
                        : error;
                    return result;
                }
                result.ClientKey = string.IsNullOrWhiteSpace(newKey) ? clientKey : newKey;

                Dictionary<string, object> response = SendRequest(ws,
                    "ssap://com.webos.service.networkinput/getPointerInputSocket",
                    new Dictionary<string, object>(), 6000);
                string socketPath = null;
                object payloadObject;
                if (response != null && response.TryGetValue("payload", out payloadObject))
                {
                    Dictionary<string, object> payloadDict =
                        payloadObject as Dictionary<string, object>;
                    if (payloadDict != null)
                        socketPath = GetString(payloadDict, "socketPath");
                }
                if (string.IsNullOrWhiteSpace(socketPath))
                {
                    result.Error = "La TV no devolvio el canal de entrada (socketPath).";
                    Log.Write("Boton " + buttonName + ": sin socketPath en la respuesta.");
                    return result;
                }

                string inputUrl;
                if (socketPath.StartsWith("ws://", StringComparison.OrdinalIgnoreCase) ||
                    socketPath.StartsWith("wss://", StringComparison.OrdinalIgnoreCase))
                {
                    inputUrl = socketPath;
                }
                else
                {
                    string host = ip.Contains(":") ? "[" + ip + "]" : ip;
                    string path = socketPath.StartsWith("/") ? socketPath : "/" + socketPath;
                    inputUrl = "ws://" + host + ":" + Port + path;
                }

                input = ConnectSocket(new Uri(inputUrl), 5000);
                SendText(input, "type:button\nname:" + buttonName + "\n\n");
                Thread.Sleep(150);
                result.Ok = true;
                return result;
            }
            catch (Exception ex)
            {
                result.Error = FriendlyError(ex, ip);
                Log.Write("Error en boton " + buttonName + ": " + ex);
                return result;
            }
            finally
            {
                if (input != null) { try { input.Dispose(); } catch { } }
                if (ws != null) { try { ws.Dispose(); } catch { } }
            }
        }

        private static string FriendlyError(Exception ex, string ip)
        {
            if (ex is OperationCanceledException || ex is WebSocketException || ex is SocketException)
                return "No se pudo conectar con la TV (" + ip + "). Comprueba que esta encendida, " +
                       "en la misma red y que la IP es correcta.";
            return "Error de conexion con la TV: " + ex.Message;
        }
    }
}

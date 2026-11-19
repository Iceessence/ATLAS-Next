using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AtlasNext;

internal sealed record ReferenceModel(string Name,string Digest);
internal sealed record ReferenceTurn(string Role,string Content);

internal sealed class LocalReference : IDisposable
{
    internal const string Address="http://127.0.0.1:11434/";
    private const int ResponseLimit=256*1024;
    private readonly HttpClient client;

    internal LocalReference() : this(new SocketsHttpHandler {
        UseProxy=false,AllowAutoRedirect=false,UseCookies=false,
        ConnectCallback=async (context, token)=>{
            if(context.DnsEndPoint.Host!="127.0.0.1" || context.DnsEndPoint.Port!=11434)
                throw new InvalidOperationException("The reference service is restricted to IPv4 loopback.");
            var socket=new Socket(AddressFamily.InterNetwork,SocketType.Stream,ProtocolType.Tcp);
            try {
                await socket.ConnectAsync(new IPEndPoint(IPAddress.Loopback,11434),token);
                return new NetworkStream(socket,ownsSocket:true);
            } catch { socket.Dispose(); throw; }
        }
    }) { }

    internal LocalReference(HttpMessageHandler handler)
    {
        client=new(handler) {BaseAddress=new Uri(Address),Timeout=Timeout.InfiniteTimeSpan};
    }

    public async Task<ReferenceModel[]> Discover(CancellationToken token)
    {
        using var deadline=CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(TimeSpan.FromSeconds(8));
        return await ReadModels(deadline.Token);
    }

    private async Task<ReferenceModel[]> ReadModels(CancellationToken token)
    {
        using var request=new HttpRequestMessage(HttpMethod.Get,"api/tags");
        using var doc=await Read(request,token);
        if(!doc.RootElement.TryGetProperty("models",out var rows) || rows.ValueKind!=JsonValueKind.Array)
            throw new InvalidDataException("Invalid model inventory.");
        var models=new Dictionary<string,ReferenceModel>(StringComparer.Ordinal);
        foreach(var row in rows.EnumerateArray()) {
            if(row.ValueKind!=JsonValueKind.Object) throw new InvalidDataException("Invalid model inventory.");
            var name=Text(row,"name");
            var digest=Text(row,"digest");
            if(name==null || !Regex.IsMatch(name,@"\A[a-zA-Z0-9._-]+:[a-zA-Z0-9._-]+\z") ||
                name.Contains("cloud",StringComparison.OrdinalIgnoreCase) || digest is not {Length:64} ||
                !digest.All(Uri.IsHexDigit) || row.TryGetProperty("remote_host",out _) || row.TryGetProperty("remote_model",out _) ||
                !row.TryGetProperty("size",out var size) || size.ValueKind!=JsonValueKind.Number || !size.TryGetInt64(out long bytes) || bytes<=0 ||
                !row.TryGetProperty("details",out var details) || details.ValueKind!=JsonValueKind.Object ||
                Text(details,"format")!="gguf") continue;
            if(!models.TryAdd(name,new(name,digest.ToLowerInvariant()))) throw new InvalidDataException("Duplicate local model name.");
        }
        return models.Values.OrderBy(model=>model.Name,StringComparer.Ordinal).ToArray();
    }

    public async Task<string> Reply(ReferenceModel selected,IReadOnlyList<ReferenceTurn> messages,CancellationToken token)
    {
        if(messages.Count is <1 or >11 || messages.Count%2==0 ||
            messages.Where((turn,index)=>turn.Role!=(index%2==0 ? "user" : "assistant")).Any() ||
            messages.Any(turn=>string.IsNullOrWhiteSpace(turn.Content) || turn.Content.Length>4096) ||
            messages.Sum(turn=>turn.Content.Length)>12000)
            throw new ArgumentException("Start a new conversation or shorten the message. The context limit is 12,000 characters.");

        using var deadline=CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(TimeSpan.FromSeconds(60));
        if(!(await ReadModels(deadline.Token)).Contains(selected))
            throw new InvalidDataException("The selected local model is missing or changed. Refresh the model list.");
        using var request=new HttpRequestMessage(HttpMethod.Post,"api/chat") {
            Content=JsonContent.Create(new {model=selected.Name,messages,stream=false,think=false,keep_alive=0,
                options=new {temperature=0,seed=42,num_ctx=4096,num_predict=768}})
        };
        using var doc=await Read(request,deadline.Token);
        var root=doc.RootElement;
        if(Text(root,"done_reason")=="length")
            throw new InvalidDataException("The model reached the reply limit. Ask for a shorter result.");
        if(Text(root,"model")!=selected.Name || !root.TryGetProperty("done",out var done) || done.ValueKind!=JsonValueKind.True ||
            !root.TryGetProperty("message",out var message) || message.ValueKind!=JsonValueKind.Object ||
            Text(message,"role")!="assistant" || message.TryGetProperty("tool_calls",out _) ||
            Text(message,"content") is not {Length:>0 and <=4096} content || string.IsNullOrWhiteSpace(content))
            throw new InvalidDataException("Incomplete or invalid local model reply.");
        return content;
    }

    private async Task<JsonDocument> Read(HttpRequestMessage request,CancellationToken token)
    {
        using var response=await client.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,token);
        if(!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Local reference service returned HTTP {(int)response.StatusCode}.");
        if(response.Content.Headers.ContentLength>ResponseLimit) throw new InvalidDataException("Local response exceeded its limit.");
        using var stream=await response.Content.ReadAsStreamAsync(token);
        using var buffer=new MemoryStream();
        byte[] chunk=new byte[8192];
        while(true) {
            int length=await stream.ReadAsync(chunk,token);
            if(length==0) break;
            if(buffer.Length+length>ResponseLimit) throw new InvalidDataException("Local response exceeded its limit.");
            buffer.Write(chunk,0,length);
        }
        var doc=JsonDocument.Parse(buffer.GetBuffer().AsMemory(0,(int)buffer.Length));
        if(doc.RootElement.ValueKind==JsonValueKind.Object) return doc;
        doc.Dispose();
        throw new InvalidDataException("Invalid local service response.");
    }

    private static string? Text(JsonElement root,string name)=>
        root.TryGetProperty(name,out var value) && value.ValueKind==JsonValueKind.String ? value.GetString() : null;
    public void Dispose()=>client.Dispose();
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace Mini32Lab
{
    public sealed class ObsClient : IDisposable
    {
        private readonly ClientWebSocket socket=new ClientWebSocket();
        private readonly SemaphoreSlim gate=new SemaphoreSlim(1,1);
        private readonly CancellationTokenSource lifetime=new CancellationTokenSource();
        public bool Connected { get {return socket.State==WebSocketState.Open && identified;} }
        private bool identified;
        public static string Auth(string password,string salt,string challenge)
        {
            using(var hash=SHA256.Create()) {
                string secret=Convert.ToBase64String(hash.ComputeHash(Encoding.UTF8.GetBytes(password+salt)));
                return Convert.ToBase64String(hash.ComputeHash(Encoding.UTF8.GetBytes(secret+challenge)));
            }
        }
        private async Task Send(int op,object data,CancellationToken token)
        {
            string json=new JavaScriptSerializer().Serialize(new {op=op,d=data});
            byte[] bytes=Encoding.UTF8.GetBytes(json);
            await socket.SendAsync(new ArraySegment<byte>(bytes),WebSocketMessageType.Text,true,token).ConfigureAwait(false);
        }
        private async Task<Dictionary<string,object>> Receive(CancellationToken token)
        {
            byte[] bytes=new byte[8192];using(var buffer=new MemoryStream()) {
                WebSocketReceiveResult part;
                do {
                    part=await socket.ReceiveAsync(new ArraySegment<byte>(bytes),token).ConfigureAwait(false);
                    if(part.MessageType==WebSocketMessageType.Close)throw new IOException("OBS encerrou a conexao. Confira a senha e reconecte.");
                    if(part.MessageType!=WebSocketMessageType.Text)throw new IOException("Resposta OBS nao e JSON textual.");
                    buffer.Write(bytes,0,part.Count);if(buffer.Length>2000000)throw new IOException("Resposta OBS muito grande.");
                }while(!part.EndOfMessage);
                return new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(Encoding.UTF8.GetString(buffer.ToArray()));
            }
        }
        public async Task Connect(int port,string password)
        {
            using(var timeout=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token)) {
                timeout.CancelAfter(6000);
                try {
                    await socket.ConnectAsync(new Uri("ws://127.0.0.1:"+port),timeout.Token).ConfigureAwait(false);
                    var hello=await Receive(timeout.Token).ConfigureAwait(false);
                    if(Convert.ToInt32(hello["op"])!=0)throw new IOException("OBS nao enviou a saudacao esperada.");
                    var data=(Dictionary<string,object>)hello["d"];
                    var identify=new Dictionary<string,object>{{"rpcVersion",1},{"eventSubscriptions",0}};
                    if(data.ContainsKey("authentication")) {var a=(Dictionary<string,object>)data["authentication"];identify["authentication"]=Auth(password,(string)a["salt"],(string)a["challenge"]);}
                    await Send(1,identify,timeout.Token).ConfigureAwait(false);
                    var reply=await Receive(timeout.Token).ConfigureAwait(false);
                    if(Convert.ToInt32(reply["op"])!=2)throw new IOException("OBS nao confirmou a autenticacao.");
                    identified=true;
                }catch{Dispose();throw;}
            }
        }
        public async Task<Dictionary<string,object>> Request(string type,object data)
        {
            if(!Connected)throw new IOException("Conecte ao OBS primeiro.");
            using(var timeout=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token)) {
                timeout.CancelAfter(5000);
                await gate.WaitAsync(timeout.Token).ConfigureAwait(false);
                try {
                    string id=Guid.NewGuid().ToString("N");
                    await Send(6,new {requestType=type,requestId=id,requestData=data},timeout.Token).ConfigureAwait(false);
                    while(true) {
                        var reply=await Receive(timeout.Token).ConfigureAwait(false);
                        if(Convert.ToInt32(reply["op"])!=7)continue;
                        var d=(Dictionary<string,object>)reply["d"];
                        if(Convert.ToString(d["requestId"])!=id)continue;
                        var status=(Dictionary<string,object>)d["requestStatus"];
                        if(!Convert.ToBoolean(status["result"]))throw new ObsRequestException("OBS recusou "+type+": "+(status.ContainsKey("comment")?status["comment"]:status["code"]));
                        return d.ContainsKey("responseData")?(Dictionary<string,object>)d["responseData"]:new Dictionary<string,object>();
                    }
                }catch(ObsRequestException){throw;}
                catch {Dispose();throw;}
                finally{gate.Release();}
            }
        }
        public void Dispose() {identified=false;try{lifetime.Cancel();socket.Abort();socket.Dispose();}catch(ObjectDisposedException){} }
    }
    public sealed class ObsRequestException : Exception {public ObsRequestException(string message):base(message){} }
}

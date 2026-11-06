using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;

namespace Mini32
{
    public sealed class Binding { public int Note=53; public int Channel=1; public string ActionId=""; public int? Volume=null; public string IconFile=""; }
    public sealed class Bank
    {
        public string Id=Guid.NewGuid().ToString("N"); public string Name="Novo banco";
        public List<Binding> Keys=new List<Binding>();
        public static Bank Create(string name) { var b=new Bank{Name=name}; for(int i=0;i<32;i++)b.Keys.Add(new Binding{Note=53+i}); return b; }
        public override string ToString(){return Name;}
    }
    public sealed class ActionDef
    {
        public string Id=Guid.NewGuid().ToString("N"),Name="Nova ação",Kind="audio",AssetId="",Target="",Behavior="trigger",Group="";
        public int Volume=75,FadeIn=0,FadeOut=80;
        public double Start=0,End=0;
        public bool Pitch=true,Loop=false,Music=false,Duck=false;
        public ActionDef Copy(){var j=new JavaScriptSerializer();var a=j.Deserialize<ActionDef>(j.Serialize(this));a.Id=Guid.NewGuid().ToString("N");return a;}
        public override string ToString(){return Name;}
    }
    public sealed class Asset
    {
        public string Id=Guid.NewGuid().ToString("N"),Name="",File=""; public double Seconds;
        public override string ToString(){return Name;}
    }
    public sealed class Settings
    {
        public int Version=2,Master=70,ObsPort=4455; public string ActiveBank="",FreeBank="",WallpaperFile="",MidiSignature="",Output="",ObsSecret="";
        public bool MidiAuto=false,ObsAuto=false;
        public List<int> FreeKeys=new List<int>();
        public List<Bank> Banks=new List<Bank>(); public List<ActionDef> Actions=new List<ActionDef>(); public List<Asset> Assets=new List<Asset>();
        public static Settings Create(){var s=new Settings();var b=Bank.Create("Live principal");s.Banks.Add(b);s.ActiveBank=b.Id;return s;}
        public void Validate()
        {
            if(Version!=2)throw new InvalidDataException("Versão de configuração não suportada.");
            if(Banks==null || Banks.Count<1 || Banks.Count>100 || Actions==null || Assets==null)throw new InvalidDataException("Configuração inválida.");
            if(Actions.Count>3200 || Assets.Count>2000 || Banks.Select(b=>b.Id).Distinct().Count()!=Banks.Count || Actions.Select(a=>a.Id).Distinct().Count()!=Actions.Count || Assets.Select(a=>a.Id).Distinct().Count()!=Assets.Count)throw new InvalidDataException("Identificadores inválidos na configuração.");
            foreach(var b in Banks){if(b.Keys==null || b.Keys.Count!=32)throw new InvalidDataException("O banco precisa ter 32 teclas.");var notes=new HashSet<int>();foreach(var k in b.Keys){if(k.Note<0 || k.Note>127 || k.Channel<1 || k.Channel>16 || !notes.Add(k.Channel*128+k.Note))throw new InvalidDataException("Notas duplicadas ou inválidas no banco.");if(k.ActionId!="" && !Actions.Any(a=>a.Id==k.ActionId))throw new InvalidDataException("Ação não encontrada.");if(k.Volume.HasValue&&(k.Volume.Value<0||k.Volume.Value>100))throw new InvalidDataException("Volume da tecla inválido.");if(k.IconFile==null)k.IconFile="";if(k.IconFile!=""&&(k.IconFile!=Path.GetFileName(k.IconFile)||!new[]{".png",".jpg",".jpeg",".webp"}.Contains(Path.GetExtension(k.IconFile).ToLowerInvariant())))throw new InvalidDataException("Imagem da tecla inválida.");}}
            foreach(var a in Actions){if(a.Volume<0||a.Volume>100||a.FadeIn<0||a.FadeIn>5000||a.FadeOut<0||a.FadeOut>5000||double.IsNaN(a.Start)||double.IsInfinity(a.Start)||double.IsNaN(a.End)||double.IsInfinity(a.End)||a.Start<0||a.End<0)throw new InvalidDataException("Parâmetros de áudio inválidos.");}
            foreach(var a in Assets)if(String.IsNullOrEmpty(a.File)||a.File!=Path.GetFileName(a.File))throw new InvalidDataException("Caminho da biblioteca inválido.");
            Master=Math.Max(0,Math.Min(100,Master));ObsPort=Math.Max(1,Math.Min(65535,ObsPort));
            if(!Banks.Any(b=>b.Id==ActiveBank))ActiveBank=Banks[0].Id;
            if(!Banks.Any(b=>b.Id==FreeBank))FreeBank=ActiveBank;
            if(FreeKeys==null)FreeKeys=new List<int>();
            FreeKeys=FreeKeys.Where(i=>i>=0&&i<32).Distinct().Take(16).ToList();
            if(FreeKeys.Count<16){var bank=Banks.First(b=>b.Id==FreeBank);foreach(int i in Enumerable.Range(0,32).OrderByDescending(i=>bank.Keys[i].ActionId!="")){if(FreeKeys.Count==16)break;if(!FreeKeys.Contains(i))FreeKeys.Add(i);}}
            if(WallpaperFile==null)WallpaperFile="";
            if(WallpaperFile!=""&&(WallpaperFile!=Path.GetFileName(WallpaperFile)||!new[]{".png",".jpg",".jpeg"}.Contains(Path.GetExtension(WallpaperFile).ToLowerInvariant())))throw new InvalidDataException("Imagem de fundo inválida.");
        }
    }
    public sealed class Store
    {
        public readonly string Root,Library,Icons,Wallpapers,Config,Logs;
        public string Recovery="";
        public Store(string root){Root=root;Library=Path.Combine(root,"library");Icons=Path.Combine(root,"icons");Wallpapers=Path.Combine(root,"wallpapers");Config=Path.Combine(root,"settings.json");Logs=Path.Combine(root,"logs");Directory.CreateDirectory(Library);Directory.CreateDirectory(Icons);Directory.CreateDirectory(Wallpapers);Directory.CreateDirectory(Logs);}
        public Settings Load()
        {
            if(!File.Exists(Config))return Settings.Create();
            try{return Read(Config);}catch(Exception e){
                string damaged=Config+".invalid-"+DateTime.Now.ToString("yyyyMMdd-HHmmssfff");File.Copy(Config,damaged,false);
                if(File.Exists(Config+".bak")){try{var s=Read(Config+".bak");Recovery="Configuração recuperada da cópia de segurança. Original preservado.";return s;}catch{}}
                throw new InvalidDataException("Não foi possível abrir suas configurações. Os arquivos foram preservados em "+Root,e);
            }
        }
        private static Settings Read(string path){var s=new JavaScriptSerializer{MaxJsonLength=4000000}.Deserialize<Settings>(File.ReadAllText(path));if(s.Version==1)s.Version=2;s.Validate();return s;}
        public void Save(Settings s){s.Validate();AtomicWrite(Config,new JavaScriptSerializer{MaxJsonLength=4000000}.Serialize(s));}
        public static void AtomicWrite(string path,string text)
        {
            string pending=path+".pending";
            using(var file=new FileStream(pending,FileMode.Create,FileAccess.Write,FileShare.None)){var bytes=new UTF8Encoding(false).GetBytes(text);file.Write(bytes,0,bytes.Length);file.Flush(true);}
            if(File.Exists(path))File.Replace(pending,path,path+".bak");else File.Move(pending,path);
        }
        public string AssetPath(Asset a){return Path.Combine(Library,a.File);}
        public static string Protect(string value){return Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(value),null,DataProtectionScope.CurrentUser));}
        public static string Unprotect(string value){if(String.IsNullOrEmpty(value))return "";return Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(value),null,DataProtectionScope.CurrentUser));}
    }
    public sealed class Log : IDisposable
    {
        private readonly object sync=new object();private StreamWriter writer;
        public Log(string folder){writer=new StreamWriter(Path.Combine(folder,"mini32-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N").Substring(0,6)+".jsonl"),false,new UTF8Encoding(false));writer.AutoFlush=true;}
        public void Write(string kind,object data){lock(sync){if(writer==null)return;try{writer.WriteLine(new JavaScriptSerializer().Serialize(new{utc=DateTimeOffset.UtcNow.ToString("o"),kind=kind,data=data}));}catch(IOException){} }}
        public void Dispose(){lock(sync){if(writer!=null)writer.Dispose();writer=null;}}
    }
}

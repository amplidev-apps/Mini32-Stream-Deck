$ErrorActionPreference = 'Stop'
# Preserve the supplied originals. Crop transparent margins only in build assets.
Add-Type -AssemblyName System.Drawing
if (-not ('Mini32BrandAssets' -as [type])) {
    Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
public static class Mini32BrandAssets {
    static Rectangle Bounds(Bitmap image) {
        int left=image.Width, top=image.Height, right=-1, bottom=-1;
        for(int y=0;y<image.Height;y++) for(int x=0;x<image.Width;x++) {
            if(image.GetPixel(x,y).A==0) continue;
            left=Math.Min(left,x); top=Math.Min(top,y);
            right=Math.Max(right,x); bottom=Math.Max(bottom,y);
        }
        if(right<0) throw new InvalidDataException("Empty branding image.");
        return Rectangle.FromLTRB(left,top,right+1,bottom+1);
    }
    public static void Build(string logoSource,string iconSource,string logoTarget,string iconTarget) {
        using(var logo=new Bitmap(logoSource))
        using(var cropped=logo.Clone(Bounds(logo),PixelFormat.Format32bppArgb))
            cropped.Save(logoTarget,ImageFormat.Png);
        int[] sizes={16,20,24,32,40,48,64,128,256};
        byte[][] frames=new byte[sizes.Length][];
        using(var original=new Bitmap(iconSource)) {
            Rectangle bounds=Bounds(original);
            for(int i=0;i<sizes.Length;i++) {
                int size=sizes[i];
                using(var bitmap=new Bitmap(size,size,PixelFormat.Format32bppArgb)) {
                    using(var g=Graphics.FromImage(bitmap)) {
                        g.Clear(Color.Transparent);
                        g.CompositingMode=CompositingMode.SourceCopy;
                        g.InterpolationMode=InterpolationMode.HighQualityBicubic;
                        g.PixelOffsetMode=PixelOffsetMode.HighQuality;
                        float scale=(size-2f)/Math.Max(bounds.Width,bounds.Height);
                        float w=bounds.Width*scale,h=bounds.Height*scale;
                        g.DrawImage(original,new RectangleF((size-w)/2,(size-h)/2,w,h),bounds,GraphicsUnit.Pixel);
                    }
                    using(var stream=new MemoryStream()) {
                        bitmap.Save(stream,ImageFormat.Png); frames[i]=stream.ToArray();
                    }
                }
            }
        }
        using(var writer=new BinaryWriter(File.Create(iconTarget))) {
            writer.Write((ushort)0);writer.Write((ushort)1);writer.Write((ushort)sizes.Length);
            int offset=6+16*sizes.Length;
            for(int i=0;i<sizes.Length;i++) {
                writer.Write((byte)(sizes[i]==256?0:sizes[i]));
                writer.Write((byte)(sizes[i]==256?0:sizes[i]));
                writer.Write((byte)0);writer.Write((byte)0);
                writer.Write((ushort)1);writer.Write((ushort)32);
                writer.Write(frames[i].Length);writer.Write(offset);offset+=frames[i].Length;
            }
            foreach(var frame in frames) writer.Write(frame);
        }
    }
}
'@
}
$root = Split-Path $PSScriptRoot -Parent
[Mini32BrandAssets]::Build(
    (Join-Path $root 'final-logo.png'),
    (Join-Path $root 'final-app-icon.png'),
    (Join-Path $PSScriptRoot 'flutter/assets/branding/final-logo.png'),
    (Join-Path $PSScriptRoot 'assets/mini32.ico'))
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'assets/mini32.ico') -Destination (Join-Path $PSScriptRoot 'flutter/windows/runner/resources/app_icon.ico') -Force
Write-Output 'Logo e icones Windows preparados a partir dos PNGs originais.'

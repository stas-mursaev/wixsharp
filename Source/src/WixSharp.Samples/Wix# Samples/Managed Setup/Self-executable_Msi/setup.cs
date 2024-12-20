using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Resources;
using System.Text;

// using Self_executable_Msi;

public static class Launcher
{
    static public int Main(string[] args)
    {
        var msi = Path.GetFullPath(@"..\..\Install Files\MyProduct.msi");
        var msiExe = Path.GetFullPath(@"..\..\MyProduct.exe");

        // Note WixSharp you do CompleSelfHostedMsi implementation in this file
        // (see below) is provided for demo purposes. The identical implementation
        // of this extension method is available from WixSharp library out of the box.
        (int exitCode, string output) = msi.CompleSelfHostedMsi(msiExe);

        if (exitCode != 0)
            Console.WriteLine(output);

        return exitCode;
    }
}

static class ExeGen
{
    public static (int exitCode, string output) CompleSelfHostedMsi(this string msiFile, string outFile)
    {
        //System.Diagnostics.Debug.Assert(false);
        var csc = LocateCsc();
        var csFile = GenerateCSharpSource(outFile + ".cs");
        try
        {
            var extraArg = "/define:DEBUG /debug+";
            Console.WriteLine($"Building: {outFile}");
            return csc.Run($"\"/res:{msiFile}\" \"-out:{outFile}\" {extraArg}  /t:winexe \"{csFile}\"", Path.GetDirectoryName(outFile));
        }
        finally
        {
            File.Delete(csFile);
        }
    }

    static string LocateCsc() =>
        Directory.GetFiles(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), @"Microsoft.NET\Framework"), "csc.exe", SearchOption.AllDirectories)
            .OrderByDescending(x => x)
            .FirstOrDefault();

    static string GenerateCSharpSource(string file)
    {
        var code = @"
using System;
using System.Resources;
using System.IO;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

class Program
{
    static int Main(string[] args)
    {
        string msi = Path.GetTempFileName();
        try
        {
            // Debug.Assert(false);
            ExtractMsi(msi);
            string msi_args = args.Any() ? string.Join("" "", args) : ""/i"";

            Process p = Process.Start(""msiexec.exe"", msi_args + ""\"""" + msi + ""\"""");
            p.WaitForExit();
            return p.ExitCode;
        }
        catch (Exception)
        {
            // report the error
            return -1;
        }
        finally
        {
            try
            {
                if (File.Exists(msi))
                    File.Delete(msi);
            }
            catch { }
        }
    }

    static void ExtractMsi(string outFile)
    {
        Assembly asm = Assembly.GetExecutingAssembly();

        using (Stream stream = asm.GetManifestResourceStream(asm.GetName().Name+"".msi""))
        {
            if (stream != null)
            {
                byte[] resourceBytes = new byte[stream.Length];
                stream.Read(resourceBytes, 0, resourceBytes.Length);

                File.WriteAllBytes(outFile, resourceBytes);
            }
            else
            {
                Console.WriteLine(""Resource not found."");
            }
        }
    }
}";
        File.WriteAllText(file, code);
        return file;
    }

    static (int exitCode, string output) Run(this string exe, string arguments, string workingDir)
    {
        using (var process = new Process())
        {
            process.StartInfo.FileName = exe;
            process.StartInfo.Arguments = arguments;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.WorkingDirectory = workingDir;
            process.StartInfo.CreateNoWindow = true;
            process.Start();

            var output = new StringBuilder();

            output.AppendLine(process.StandardOutput.ReadToEnd());
            output.AppendLine(process.StandardError.ReadToEnd());

            process.WaitForExit();
            return (process.ExitCode, output.ToString());
        }
    }
}
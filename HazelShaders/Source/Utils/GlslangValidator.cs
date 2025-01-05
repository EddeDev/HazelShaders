using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;

namespace HazelShaders
{
    internal class GlslangValidator
    {
        public static string GetGlslangValidatorPath()
        {
            string vulkanSdkPathStr = Environment.GetEnvironmentVariable("VULKAN_SDK");
            string glslangValidatorPath = Path.Combine(vulkanSdkPathStr, "Bin/glslangValidator.exe");
            if (File.Exists(glslangValidatorPath))
                return glslangValidatorPath;

            string appdataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            AssemblyName assemblyName = typeof(GlslangValidator).Assembly.GetName();
            var rootPath = Path.Combine(appdataPath, assemblyName.Name);
            glslangValidatorPath = Path.Combine(rootPath, "glslangValidator.exe");
            if (File.Exists(glslangValidatorPath))
                return glslangValidatorPath;

            var zipFilename = "glslang.zip";
            var zipPath = Path.Combine(rootPath, zipFilename);
            Directory.CreateDirectory(Path.GetDirectoryName(zipPath));

            Uri address = new Uri("https://github.com/KhronosGroup/glslang/releases/download/main-tot/glslang-master-windows-Release.zip");
            var client = new WebClient();
            client.DownloadFile(address, zipPath);
            client.Dispose();
            if (!File.Exists(zipPath))
                return "";

            var directoryPath = Path.Combine(rootPath, Path.GetFileNameWithoutExtension(zipFilename));
            ZipFile.ExtractToDirectory(zipPath, directoryPath);
            File.Delete(zipPath);

            File.Copy(Path.Combine(directoryPath, "bin/glslangValidator.exe"), glslangValidatorPath);
            Directory.Delete(directoryPath, true);
            return glslangValidatorPath;
        }
    }
}

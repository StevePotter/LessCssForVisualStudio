/***************************************************************************

Copyright (c) Microsoft Corporation. All rights reserved.
This code is licensed under the Visual Studio SDK license terms.
THIS CODE IS PROVIDED *AS IS* WITHOUT WARRANTY OF
ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING ANY
IMPLIED WARRANTIES OF FITNESS FOR A PARTICULAR
PURPOSE, MERCHANTABILITY, OR NON-INFRINGEMENT.

***************************************************************************/

using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.VisualStudio.Shell;
using VSLangProj80;

namespace Microsoft.Samples.VisualStudio.GeneratorSample
{
    /// <summary>
    /// Compiles less css into regular css files.  Leverages work done by duncanless to add lesscss command line support.
    /// </summary>
    /// <remarks>You could use the dotless library.  But I opted not to because:
    /// 1. bugs kept surfacing.  I don't want to wait on them to fix the bugs.
    /// 2. Latest release of less.js is always available...not so with dotless
    /// </remarks>
    [ComVisible(true)]
    [Guid("52A316AA-1997-4c81-9969-83604C09EEB4")]
    //have to register for every language project or else it might not work
    [CodeGeneratorRegistration(typeof (LessCss), "LessCss Compiler", vsContextGuids.vsContextGuidVCSProject,
        GeneratesDesignTimeSource = true)]
    [CodeGeneratorRegistration(typeof (LessCss), "LessCss Compiler", vsContextGuids.vsContextGuidVBProject,
        GeneratesDesignTimeSource = true)]
    [CodeGeneratorRegistration(typeof (LessCss), "LessCss Compiler", vsContextGuids.vsContextGuidVJSProject,
        GeneratesDesignTimeSource = true)]
    [ProvideObject(typeof (LessCss))]
    public class LessCss : BaseCodeGeneratorWithSite
    {
#pragma warning disable 0414
        //The name of this generator (use for 'Custom Tool' property of project item).  from steve:  no idea why this is necessary.  but it breaks if you comment this out.  and if you change this, changing the code generator name in VS doesn't work.  It's like this and the class name need to be the same.
        internal static string name = "LessCss";
#pragma warning restore 0414

        protected override string GetDefaultExtension()
        {
            //with .less extension, just make it .less.  Otherwise append the .generated pre extension
            if (Path.GetExtension(InputFilePath).ToLowerInvariant() == ".less")
                return ".css";
            return ".generated.css";
        }

        private string ExtensionFolder
        {
            get { return System.IO.Path.GetDirectoryName(typeof (LessCss).Assembly.Location); }
        }

        public static void CopyStream(Stream input, Stream output)
        {
            // Insert null checking here for production
            byte[] buffer = new byte[8192];
            int bytesRead;
            while ((bytesRead = input.Read(buffer, 0, buffer.Length)) > 0)
            {
                output.Write(buffer, 0, bytesRead);
            }
        }


        /// <summary>
        /// Function that builds the contents of the generated file based on the contents of the input file
        /// </summary>
        /// <param name="inputFileContent">Content of the input file</param>
        /// <returns>Generated file as a byte array</returns>
        protected override byte[] GenerateCode(string inputFileContent)
        {
            try
            {
                var lessJs = ExtractEmbeddedResource("less.js");
                ExtractEmbeddedResource("lessc.wsf");
                var lessCmd = ExtractEmbeddedResource("lessc.cmd");
                var outputCss = Path.GetTempFileName() + ".css";
                string errors;
                using (Process process = System.Diagnostics.Process.Start(new ProcessStartInfo(lessCmd, "\"" + InputFilePath + "\" \"" + outputCss + "\"" )
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    WorkingDirectory = Path.GetDirectoryName(lessCmd),
                }))
                {
                    errors = process.StandardError.ReadToEnd();
                    process.WaitForExit();
                    process.Close();
                }
                    if ( !string.IsNullOrWhiteSpace(errors))
                    {
                        GeneratorError(0, errors, 0, 0);
                        return null;
                    }
                var css = File.ReadAllText(outputCss);
                return ConvertToBytes(css);
            }
            catch (Exception exception)
            {
                GeneratorError(0, exception.Message, 0, 0);
            }
            return null;
        }

        /// <summary>
        /// If the file doesn't already exist, this extracts it from the assemble manifest and saves it as a file.
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        private string ExtractEmbeddedResource(string fileName)
        {
            var path = Path.Combine(ExtensionFolder, fileName);
            if (!File.Exists(fileName))
            {
                var resourceName = "LessCssCompiler." + fileName;
                var assembly = typeof (LessCss).Assembly;
                using (Stream input = assembly.GetManifestResourceStream(resourceName))
                using (Stream output = File.Create(path))
                {
                    CopyStream(input, output);
                }
            }
            return path;
        }

        /// <summary>
        /// Takes the file contents and converts them to a byte[] that VS can use to update generated code.
        /// </summary>
        /// <param name="content"></param>
        /// <returns></returns>
        private static byte[] ConvertToBytes(string content)
        {
            //Get the preamble (byte-order mark) for our encoding
            byte[] preamble = Encoding.UTF8.GetPreamble();
            int preambleLength = preamble.Length;

            byte[] body = Encoding.UTF8.GetBytes(content);

            //Prepend the preamble to body (store result in resized preamble array)
            Array.Resize(ref preamble, preambleLength + body.Length);
            Array.Copy(body, 0, preamble, preambleLength, body.Length);

            //Return the combined byte array
            return preamble;
        }

    }
}
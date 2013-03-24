﻿/* Copyright (c) 2013 Rick (rick 'at' gibbed 'dot' us)
 * 
 * This software is provided 'as-is', without any express or implied
 * warranty. In no event will the authors be held liable for any damages
 * arising from the use of this software.
 * 
 * Permission is granted to anyone to use this software for any purpose,
 * including commercial applications, and to alter it and redistribute it
 * freely, subject to the following restrictions:
 * 
 * 1. The origin of this software must not be misrepresented; you must not
 *    claim that you wrote the original software. If you use this software
 *    in a product, an acknowledgment in the product documentation would
 *    be appreciated but is not required.
 * 
 * 2. Altered source versions must be plainly marked as such, and must not
 *    be misrepresented as being the original software.
 * 
 * 3. This notice may not be removed or altered from any source
 *    distribution.
 */

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml;
using System.Xml.XPath;
using Gibbed.SimCity5.FileFormats;
using Gibbed.SimCity5.FileFormats.Variants;
using NDesk.Options;

namespace Gibbed.SimCity5.PropConvert
{
    internal class Program
    {
        private static string GetExecutableName()
        {
            return Path.GetFileName(System.Reflection.Assembly.GetExecutingAssembly().Location);
        }

        private static ProjectData.HashList<uint> _Names;

        public static void Main(string[] args)
        {
            var mode = Mode.Unknown;
            bool showHelp = false;
            string currentProject = null;

            var options = new OptionSet
            {
                {
                    "e|export", "convert from prop to XML", v =>
                    {
                        if (v != null)
                        {
                            mode = Mode.Export;
                        }
                    }
                },
                {
                    "i|import", "convert from XML to prop", v =>
                    {
                        if (v != null)
                        {
                            mode = Mode.Import;
                        }
                    }
                },
                { "p|project=", "override current project", v => currentProject = v },
                { "h|help", "show this message and exit", v => showHelp = v != null },
            };

            List<string> extras;

            try
            {
                extras = options.Parse(args);
            }
            catch (OptionException e)
            {
                Console.Write("{0}: ", GetExecutableName());
                Console.WriteLine(e.Message);
                Console.WriteLine("Try `{0} --help' for more information.", GetExecutableName());
                return;
            }

            // detect!
            if (mode == Mode.Unknown &&
                extras.Count >= 1)
            {
                var extension = Path.GetExtension(extras[0]);

                if (extension != null &&
                    extension.ToLowerInvariant() == ".xml")
                {
                    mode = Mode.Import;
                }
                else
                {
                    mode = Mode.Export;
                }
            }

            if (extras.Count < 1 || extras.Count > 2 ||
                showHelp == true ||
                mode == Mode.Unknown)
            {
                Console.WriteLine("Usage: {0} [OPTIONS]+ [-e] input_prop [output_xml]", GetExecutableName());
                Console.WriteLine("       {0} [OPTIONS]+ [-i] input_xml [output_prop]", GetExecutableName());
                Console.WriteLine();
                Console.WriteLine("Options:");
                options.WriteOptionDescriptions(Console.Out);
                return;
            }

            var manager = ProjectData.Manager.Load(currentProject);
            if (manager.ActiveProject == null)
            {
                Console.WriteLine("Warning: no active project loaded.");
            }

            Dictionary<Type, Handlers.BaseHandler> typeHandlers;
            Dictionary<string, Handlers.BaseHandler> nameHandlers;
            Handlers.HandlerFactory.GetHandlers(out typeHandlers, out nameHandlers);

            _Names = null;

            if (mode == Mode.Export)
            {
                string inputPath = extras[0];
                string outputPath = extras.Count > 1
                                        ? extras[1]
                                        : Path.ChangeExtension(inputPath, ".xml");

                var propertyListFile = new PropertyListFile();
                using (var input = File.OpenRead(inputPath))
                {
                    propertyListFile.Deserialize(input);
                }

                using (var output = File.Create(outputPath))
                {
                    var settings = new XmlWriterSettings
                    {
                        Indent = true,
                        IndentChars = "\t",
                        CheckCharacters = false,
                    };

                    var writer = XmlWriter.Create(output, settings);

                    writer.WriteStartDocument();
                    writer.WriteStartElement("properties");
                    foreach (var key in propertyListFile.Keys)
                    {
                        var variant = propertyListFile[key];
                        var type = variant.GetType();
                        if (typeHandlers.ContainsKey(type) == false)
                        {
                            throw new InvalidOperationException();
                        }
                        var handler = typeHandlers[type];

                        writer.WriteStartElement(handler.Name);
                        writer.WriteAttributeString("id", key.ToString("X8"));
                        handler.ExportVariant(variant, writer);
                        writer.WriteEndElement();
                    }
                    writer.WriteEndElement();
                    writer.WriteEndDocument();
                    writer.Flush();
                }
            }
            else if (mode == Mode.Import)
            {
                string inputPath = extras[0];
                string outputPath = extras.Count > 1
                                        ? extras[1]
                                        : Path.ChangeExtension(inputPath, ".prop");

                var propertyListFile = new PropertyListFile();
                using (var input = File.OpenRead(inputPath))
                {
                    var doc = new XPathDocument(input);
                    var nav = doc.CreateNavigator();

                    var root = nav.SelectSingleNode("/properties");
                    if (root != null)
                    {
                        var properties = root.Select("*");
                        while (properties.MoveNext() == true)
                        {
                            var property = properties.Current;
                            if (property == null)
                            {
                                throw new InvalidOperationException();
                            }

                            if (nameHandlers.ContainsKey(property.Name) == false)
                            {
                                throw new KeyNotFoundException();
                            }
                            var handler = nameHandlers[property.Name];

                            var id = property.GetAttribute("id", "");
                            if (string.IsNullOrEmpty(id) == true)
                            {
                                throw new InvalidOperationException();
                            }
                            var key = uint.Parse(id, NumberStyles.AllowHexSpecifier);

                            BaseVariant variant;
                            handler.ImportVariant(property.CreateNavigator(), out variant);
                            propertyListFile[key] = variant;
                        }
                    }
                }

                using (var output = File.Create(outputPath))
                {
                    propertyListFile.Serialize(output);
                }
            }
            else
            {
                throw new NotSupportedException();
            }
        }
    }
}

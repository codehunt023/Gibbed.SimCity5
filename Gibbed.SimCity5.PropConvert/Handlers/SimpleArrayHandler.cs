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
using System.Xml;
using System.Xml.XPath;
using Gibbed.SimCity5.FileFormats.Variants;

namespace Gibbed.SimCity5.PropConvert.Handlers
{
    internal abstract class SimpleArrayHandler<TVariant, TValue> : ArrayHandler<TVariant, TValue>
        where TVariant : ArrayVariant<TValue>, new()
    {
        protected abstract string ItemName { get; }

        protected override sealed void ExportVariant(TVariant variant, XmlWriter writer)
        {
            foreach (var item in variant.Value)
            {
                writer.WriteStartElement(this.ItemName);
                this.ExportItem(item, writer);
                writer.WriteEndElement();
            }
        }

        protected abstract void ExportItem(TValue value, XmlWriter writer);

        protected override sealed void ImportVariant(XPathNavigator nav, out TVariant variant)
        {
            var output = new List<TValue>();
            var input = nav.Select(this.ItemName);
            while (input.MoveNext() == true)
            {
                var item = input.Current;
                if (item == null)
                {
                    throw new InvalidOperationException();
                }

                TValue dummy;
                this.ImportItem(item.CreateNavigator(), out dummy);
                output.Add(dummy);
            }

            variant = new TVariant()
            {
                Value = output,
            };
        }

        protected abstract void ImportItem(XPathNavigator nav, out TValue value);
    }
}

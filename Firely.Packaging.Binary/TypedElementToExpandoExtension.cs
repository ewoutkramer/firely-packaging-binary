using Hl7.Fhir.ElementModel;
using Hl7.Fhir.Model;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Text;

namespace Firely.Packaging.Binary
{
    public static class TypedElementToExpandoExtension
    {
        public static ExpandoObject ToExpando(this ITypedElement element)
        {
            var result = new ExpandoObject();

            if(element.Value is not null)
            {
                result.TryAdd("value", element.Value);
            }

            var children = element.Children().ToArray();
            for(int ix = 0; ix < children.Length; ix++)
            {
                if(children[ix].Definition.IsCollection)
                {
                    var childlist = new List<ExpandoObject>();
                    var collectionName = children[ix].Name;
                    do
                    {
                        childlist.Add(children[ix].ToExpando());
                        ix++;
                    }
                    while (ix < children.Length && children[ix].Name == collectionName);

                    result.TryAdd(collectionName, childlist);
                }
                else
                {
                    result.TryAdd(children[ix].Name, children[ix].ToExpando());
                }
            }

            return result;
        }
    }
}

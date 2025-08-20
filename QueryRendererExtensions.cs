using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using Unleasharp.DB.Base.QueryBuilding;
using Unleasharp.ExtensionMethods;

namespace Unleasharp.DB.MySQL;

public static class QueryRendererExtensions {
    public static string Render(this FieldSelector fragment) {
        List<string> toRender = new List<string>();

        if (!string.IsNullOrWhiteSpace(fragment.Table)) {
            if (fragment.Escape) {
                toRender.Add(Query.FieldDelimiter + fragment.Table + Query.FieldDelimiter);
            }
            else {
                toRender.Add(fragment.Table);
            }
        }

        if (!string.IsNullOrWhiteSpace(fragment.Field)) {
            if (fragment.Escape) {
                toRender.Add(Query.FieldDelimiter + fragment.Field + Query.FieldDelimiter);
            }
            else {
                toRender.Add(fragment.Field);
            }
        }

        return String.Join('.', toRender);
    }
}

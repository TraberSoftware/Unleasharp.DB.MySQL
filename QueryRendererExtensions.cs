using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using Unleasharp.DB.Base.QueryBuilding;
using Unleasharp.ExtensionMethods;

namespace Unleasharp.DB.MySQL {
    public static class QueryRendererExtensions {
        public static string Render(this FieldSelector Fragment) {
            List<string> ToRender = new List<string>();

            if (!string.IsNullOrWhiteSpace(Fragment.Table)) {
                if (Fragment.Escape) {
                    ToRender.Add(Query.FieldDelimiter + Fragment.Table + Query.FieldDelimiter);
                }
                else {
                    ToRender.Add(Fragment.Table);
                }
            }

            if (!string.IsNullOrWhiteSpace(Fragment.Field)) {
                if (Fragment.Escape) {
                    ToRender.Add(Query.FieldDelimiter + Fragment.Field + Query.FieldDelimiter);
                }
                else {
                    ToRender.Add(Fragment.Field);
                }
            }

            return String.Join('.', ToRender);
        }
    }
}

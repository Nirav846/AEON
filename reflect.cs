using System;
using System.Linq;
using System.Reflection;
using System.IO;
class P { static void Main() {
  var dir = Environment.GetEnvironmentVariable("DLLDIR");
  var asm = Assembly.LoadFrom(Path.Combine(dir, "Microsoft.Maui.Essentials.dll"));
  foreach (var t in asm.GetExportedTypes().Where(t => t.Name.Contains("Share")).OrderBy(t=>t.Name)) {
    Console.WriteLine("TYPE " + t.FullName);
    foreach (var m in t.GetMethods(BindingFlags.Public|BindingFlags.Instance|BindingFlags.Static|BindingFlags.DeclaredOnly))
      Console.WriteLine("  M " + m);
    foreach (var p in t.GetProperties(BindingFlags.Public|BindingFlags.Instance|BindingFlags.Static|BindingFlags.DeclaredOnly))
      Console.WriteLine("  P " + p.PropertyType.Name + " " + p.Name);
  }
} }

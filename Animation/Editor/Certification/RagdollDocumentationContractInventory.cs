using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using UnityEngine.Serialization;

namespace Hairibar.Ragdoll.Animation.Editor
{
    internal sealed class RagdollDocumentationContractItem
    {
        public string InventoryKind;
        public string MemberId;
        public string Symbol;
        public string DeclaringType;
        public string MemberName;
        public string MemberKind;
        public string DocumentationSection;
        public string OfficialSourceUrl;
        public string OldSerializedName;
        public string SourcePath;
        public string SourceSha256;
    }

    /// <summary>
    /// The single deterministic inventory definition for J07. Compatibility APIs
    /// opt in on their runtime declaration; serialized migrations are discovered
    /// independently from every compiled package Runtime assembly.
    /// </summary>
    internal static class RagdollDocumentationContractInventory
    {
        const string CompatibilityAttributeName =
            "Hairibar.Ragdoll.Animation.RagdollCompatibilityApiAttribute";
        const string MigrationSection = "Serialization migrations";
        const string MigrationSource =
            "https://docs.unity3d.com/ScriptReference/Serialization.FormerlySerializedAsAttribute.html";

        internal static bool TryBuild(
            string packageRoot,
            out RagdollDocumentationContractItem[] items,
            out string error)
        {
            var result = new List<RagdollDocumentationContractItem>();
            string root = Path.GetFullPath(packageRoot);
            string[] runtimeAssemblyNames = Directory.GetFiles(
                    root, "*.asmdef", SearchOption.AllDirectories)
                .Where(path => IsUnderRuntime(root, path))
                .Select(ReadAssemblyName)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();
            if (runtimeAssemblyNames.Length == 0)
            {
                items = Array.Empty<RagdollDocumentationContractItem>();
                error = "No Runtime asmdef was found.";
                return false;
            }

            var loaded = AppDomain.CurrentDomain.GetAssemblies()
                .GroupBy(assembly => assembly.GetName().Name,
                    StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(),
                    StringComparer.Ordinal);
            foreach (string assemblyName in runtimeAssemblyNames)
            {
                Assembly assembly;
                if (!loaded.TryGetValue(assemblyName, out assembly))
                {
                    try { assembly = Assembly.Load(assemblyName); }
                    catch (Exception exception)
                    {
                        items = Array.Empty<RagdollDocumentationContractItem>();
                        error = "Runtime assembly is not loaded: " + assemblyName
                            + " (" + exception.GetType().Name + ").";
                        return false;
                    }
                }

                Type[] types;
                try { types = assembly.GetTypes(); }
                catch (ReflectionTypeLoadException exception)
                {
                    items = Array.Empty<RagdollDocumentationContractItem>();
                    error = "Runtime types could not be loaded: " + assemblyName
                        + " (" + exception.Message + ").";
                    return false;
                }

                foreach (Type type in types.OrderBy(value => value.FullName,
                    StringComparer.Ordinal))
                {
                    AddCompatibilityType(root, result, type);
                    const BindingFlags Declared = BindingFlags.DeclaredOnly
                        | BindingFlags.Instance | BindingFlags.Static
                        | BindingFlags.Public | BindingFlags.NonPublic;
                    MemberInfo[] members = type.GetMembers(Declared)
                        .OrderBy(MemberIdentity, StringComparer.Ordinal).ToArray();
                    foreach (MemberInfo member in members)
                    {
                        if (member.IsDefined(typeof(ObsoleteAttribute), false)
                            && IsPublicApi(member)
                            && CompatibilityAttribute(member) == null)
                        {
                            items = Array.Empty<RagdollDocumentationContractItem>();
                            error = "Obsolete public Runtime API lacks compatibility metadata: "
                                + MemberIdentity(member) + ".";
                            return false;
                        }
                        AddCompatibilityMember(root, result, member);
                    }
                    foreach (FieldInfo field in type.GetFields(Declared)
                        .OrderBy(value => value.Name, StringComparer.Ordinal))
                    {
                        FormerlySerializedAsAttribute[] migrations = field
                            .GetCustomAttributes(typeof(FormerlySerializedAsAttribute), false)
                            .Cast<FormerlySerializedAsAttribute>()
                            .OrderBy(value => value.oldName, StringComparer.Ordinal)
                            .ToArray();
                        foreach (FormerlySerializedAsAttribute migration in migrations)
                        {
                            AddMigration(root, result, field, migration.oldName);
                        }
                    }
                }
            }

            RagdollDocumentationContractItem[] ordered = result
                .OrderBy(item => item.MemberId, StringComparer.Ordinal).ToArray();
            string duplicate = ordered.GroupBy(item => item.MemberId,
                    StringComparer.Ordinal)
                .Where(group => group.Count() != 1)
                .Select(group => group.Key).FirstOrDefault();
            if (!string.IsNullOrEmpty(duplicate))
            {
                items = ordered;
                error = "Duplicate documentation inventory identity: " + duplicate;
                return false;
            }
            RagdollDocumentationContractItem incomplete = ordered.FirstOrDefault(item =>
                string.IsNullOrWhiteSpace(item.InventoryKind)
                || string.IsNullOrWhiteSpace(item.MemberId)
                || string.IsNullOrWhiteSpace(item.Symbol)
                || string.IsNullOrWhiteSpace(item.DeclaringType)
                || string.IsNullOrWhiteSpace(item.MemberName)
                || string.IsNullOrWhiteSpace(item.MemberKind)
                || string.IsNullOrWhiteSpace(item.DocumentationSection)
                || string.IsNullOrWhiteSpace(item.OfficialSourceUrl)
                || string.IsNullOrWhiteSpace(item.SourcePath)
                || !File.Exists(item.SourcePath)
                || string.IsNullOrWhiteSpace(item.SourceSha256));
            if (incomplete != null)
            {
                items = ordered;
                error = "Incomplete documentation inventory item: "
                    + incomplete.MemberId + ".";
                return false;
            }
            if (ordered.Count(item => item.InventoryKind == "CompatibilityApi") == 0)
            {
                items = ordered;
                error = "No compatibility API metadata was discovered.";
                return false;
            }
            string[] runtimeSources = Directory.GetFiles(
                    root, "*.cs", SearchOption.AllDirectories)
                .Where(path => IsUnderRuntime(root, path)).ToArray();
            int declaredCompatibility = runtimeSources
                .Where(path => !path.EndsWith(
                    "RagdollCompatibilityApiAttribute.cs",
                    StringComparison.OrdinalIgnoreCase))
                .Sum(path => CountOccurrences(File.ReadAllText(path),
                    "[RagdollCompatibilityApi("));
            int declaredMigrations = runtimeSources.Sum(path =>
                CountOccurrences(File.ReadAllText(path),
                    "FormerlySerializedAs("));
            int reflectedCompatibility = ordered.Count(item =>
                item.InventoryKind == "CompatibilityApi");
            int reflectedMigrations = ordered.Count(item =>
                item.InventoryKind == "SerializationMigration");
            if (declaredCompatibility != reflectedCompatibility)
            {
                items = ordered;
                error = "Runtime compatibility metadata source/reflection mismatch: "
                    + declaredCompatibility + "/" + reflectedCompatibility + ".";
                return false;
            }
            if (declaredMigrations != reflectedMigrations)
            {
                items = ordered;
                error = "Runtime FormerlySerializedAs source/reflection mismatch: "
                    + declaredMigrations + "/" + reflectedMigrations + ".";
                return false;
            }
            items = ordered;
            error = string.Empty;
            return true;
        }

        internal static string ComputeInventorySha256(
            IEnumerable<RagdollDocumentationContractItem> items)
        {
            string canonical = string.Join("\n", items
                .OrderBy(item => item.MemberId, StringComparer.Ordinal)
                .Select(item => string.Join("|", item.InventoryKind, item.MemberId,
                    item.DocumentationSection, item.OfficialSourceUrl,
                    item.OldSerializedName ?? string.Empty,
                    item.SourceSha256 ?? string.Empty)));
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(canonical));
                return string.Concat(hash.Select(value => value.ToString("x2")));
            }
        }

        static void AddCompatibilityType(
            string root,
            List<RagdollDocumentationContractItem> result,
            Type type)
        {
            object attribute = CompatibilityAttribute(type);
            if (attribute == null) return;
            if (!(type.IsPublic || type.IsNestedPublic))
                throw new InvalidOperationException(
                    "Compatibility metadata is only valid on public API: "
                    + MemberIdentity(type));
            AddCompatibility(root, result, type, attribute);
        }

        static void AddCompatibilityMember(
            string root,
            List<RagdollDocumentationContractItem> result,
            MemberInfo member)
        {
            object attribute = CompatibilityAttribute(member);
            if (attribute == null) return;
            bool isPublic = IsPublicApi(member);
            if (!isPublic)
                throw new InvalidOperationException(
                    "Compatibility metadata is only valid on public API: "
                    + MemberIdentity(member));
            AddCompatibility(root, result, member, attribute);
        }

        static void AddCompatibility(
            string root,
            List<RagdollDocumentationContractItem> result,
            MemberInfo member,
            object attribute)
        {
            Type attributeType = attribute.GetType();
            string section = (string)attributeType.GetProperty(
                "DocumentationSection")?.GetValue(attribute);
            string sourceUrl = (string)attributeType.GetProperty(
                "OfficialSourceUrl")?.GetValue(attribute);
            string sourcePath = FindSource(root, member, null);
            result.Add(new RagdollDocumentationContractItem
            {
                InventoryKind = "CompatibilityApi",
                MemberId = MemberIdentity(member),
                Symbol = member is Type type ? type.Name : member.Name,
                DeclaringType = member is Type declaredType
                    ? declaredType.FullName : member.DeclaringType?.FullName,
                MemberName = member is Type namedType ? namedType.Name : member.Name,
                MemberKind = member is Type ? "Type" : member.MemberType.ToString(),
                DocumentationSection = section,
                OfficialSourceUrl = sourceUrl,
                OldSerializedName = string.Empty,
                SourcePath = sourcePath,
                SourceSha256 = File.Exists(sourcePath) ? Sha256(sourcePath) : string.Empty
            });
        }

        static void AddMigration(
            string root,
            List<RagdollDocumentationContractItem> result,
            FieldInfo field,
            string oldName)
        {
            string sourcePath = FindSource(root, field, oldName);
            result.Add(new RagdollDocumentationContractItem
            {
                InventoryKind = "SerializationMigration",
                MemberId = MemberIdentity(field) + "<-" + oldName,
                Symbol = field.Name + " <- " + oldName,
                DeclaringType = field.DeclaringType?.FullName,
                MemberName = field.Name,
                MemberKind = "Field",
                DocumentationSection = MigrationSection,
                OfficialSourceUrl = MigrationSource,
                OldSerializedName = oldName,
                SourcePath = sourcePath,
                SourceSha256 = File.Exists(sourcePath) ? Sha256(sourcePath) : string.Empty
            });
        }

        static object CompatibilityAttribute(MemberInfo member)
        {
            return member.GetCustomAttributes(false).FirstOrDefault(attribute =>
                string.Equals(attribute.GetType().FullName,
                    CompatibilityAttributeName, StringComparison.Ordinal));
        }

        static bool IsPublicApi(MemberInfo member)
        {
            return member is MethodBase method ? method.IsPublic
                : member is FieldInfo field ? field.IsPublic
                : member is PropertyInfo property
                    ? (property.GetMethod != null && property.GetMethod.IsPublic)
                        || (property.SetMethod != null && property.SetMethod.IsPublic)
                : member is EventInfo eventInfo
                    && ((eventInfo.AddMethod != null && eventInfo.AddMethod.IsPublic)
                        || (eventInfo.RemoveMethod != null
                            && eventInfo.RemoveMethod.IsPublic));
        }

        internal static string MemberIdentity(MemberInfo member)
        {
            if (member is Type type)
                return type.Assembly.GetName().Name + "::" + type.FullName + "::Type";
            string prefix = member.DeclaringType.Assembly.GetName().Name + "::"
                + member.DeclaringType.FullName + "::" + member.MemberType + "::"
                + member.Name;
            if (member is MethodInfo method)
                return prefix + "(" + string.Join(",", method.GetParameters()
                    .Select(parameter => TypeIdentity(parameter.ParameterType)))
                    + ")->" + TypeIdentity(method.ReturnType);
            if (member is PropertyInfo property)
                return prefix + "(" + string.Join(",", property.GetIndexParameters()
                    .Select(parameter => TypeIdentity(parameter.ParameterType)))
                    + ")->" + TypeIdentity(property.PropertyType);
            if (member is EventInfo eventInfo)
                return prefix + "->" + TypeIdentity(eventInfo.EventHandlerType);
            if (member is FieldInfo field)
                return prefix + "->" + TypeIdentity(field.FieldType);
            return prefix;
        }

        static string TypeIdentity(Type type)
        {
            if (type == null) return string.Empty;
            if (type.IsByRef) return TypeIdentity(type.GetElementType()) + "&";
            if (type.IsArray) return TypeIdentity(type.GetElementType()) + "[]";
            if (!type.IsGenericType) return type.FullName ?? type.Name;
            string definition = type.GetGenericTypeDefinition().FullName;
            int tick = definition.IndexOf('`');
            if (tick >= 0) definition = definition.Substring(0, tick);
            return definition + "<" + string.Join(",",
                type.GetGenericArguments().Select(TypeIdentity)) + ">";
        }

        static bool IsUnderRuntime(string root, string path)
        {
            string relative = Path.GetFullPath(path).Substring(root.Length)
                .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string[] parts = relative.Split(Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar);
            return parts.Any(part => string.Equals(part, "Runtime",
                       StringComparison.OrdinalIgnoreCase))
                && !parts.Any(part => string.Equals(part, "Tests",
                       StringComparison.OrdinalIgnoreCase)
                    || string.Equals(part, "Samples~",
                       StringComparison.OrdinalIgnoreCase));
        }

        static string ReadAssemblyName(string asmdef)
        {
            string json = File.ReadAllText(asmdef);
            const string token = "\"name\"";
            int key = json.IndexOf(token, StringComparison.Ordinal);
            int colon = key < 0 ? -1 : json.IndexOf(':', key + token.Length);
            int first = colon < 0 ? -1 : json.IndexOf('"', colon + 1);
            int last = first < 0 ? -1 : json.IndexOf('"', first + 1);
            return first >= 0 && last > first
                ? json.Substring(first + 1, last - first - 1) : string.Empty;
        }

        static string FindSource(string root, MemberInfo member, string oldName)
        {
            string typeName = member is Type type ? type.Name : member.DeclaringType.Name;
            string memberName = member is Type ? typeName : member.Name;
            string[] files = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories)
                .Where(path => IsUnderRuntime(root, path))
                .OrderBy(path => path, StringComparer.Ordinal).ToArray();
            foreach (string path in files)
            {
                string source = File.ReadAllText(path);
                if (source.IndexOf(typeName, StringComparison.Ordinal) < 0
                    || source.IndexOf(memberName, StringComparison.Ordinal) < 0)
                    continue;
                if (string.IsNullOrEmpty(oldName))
                {
                    if (source.IndexOf("[RagdollCompatibilityApi(",
                            StringComparison.Ordinal) < 0)
                        continue;
                }
                else if (source.IndexOf(
                    "FormerlySerializedAs(\"" + oldName + "\")",
                    StringComparison.Ordinal) < 0) continue;
                return Path.GetFullPath(path);
            }
            return string.Empty;
        }

        static string Sha256(string path)
        {
            using (SHA256 sha = SHA256.Create())
            using (FileStream stream = File.OpenRead(path))
                return string.Concat(sha.ComputeHash(stream)
                    .Select(value => value.ToString("x2")));
        }

        static int CountOccurrences(string text, string value)
        {
            int count = 0;
            int offset = 0;
            while ((offset = text.IndexOf(value, offset,
                StringComparison.Ordinal)) >= 0)
            {
                count++;
                offset += value.Length;
            }
            return count;
        }
    }
}

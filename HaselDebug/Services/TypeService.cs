using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Utility;
using FFXIVClientStructs.Attributes;
using FFXIVClientStructs.Expansion;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using HaselDebug.Utils;

namespace HaselDebug.Services;

[AutoConstruct]
[RegisterSingleton(Duplicate = DuplicateStrategy.Append, Registration = RegistrationStrategy.SelfWithProxyFactory)]
public partial class TypeService : IHostedService
{
    private readonly ILogger<TypeService> _logger;
    private readonly IDalamudPluginInterface _pluginInterface;
    private readonly Dictionary<Type, OrderedDictionary<int, (string, Type?)>> _offsetTypeStructs = [];
    private readonly TaskCompletionSource _loadedTcs = new();
    private readonly Dictionary<Type, StructField[]> structFields = [];

    public Task Loaded => _loadedTcs.Task;
    public ImmutableSortedDictionary<string, Type>? CSTypes { get; private set; }
    public ImmutableSortedDictionary<string, Type>? AddonTypes { get; private set; }
    public ImmutableSortedDictionary<AgentId, Type>? AgentTypes { get; private set; }
    public ImmutableDictionary<Type, (Type Original, Type Expansion)> StructViews { get; private set; } = ImmutableDictionary<Type, (Type Original, Type Expansion)>.Empty;
    public ConcurrentDictionary<nint, Type>? CustomNodeTypes { get; private set; }
    public Type[] Instances { get; private set; } = [];

    public Task StartAsync(CancellationToken cancellationToken)
    {
        Type[] types = [.. typeof(AddonAttribute).Assembly.GetTypes(), .. typeof(ExpansionInterop).Assembly.GetTypes()];

        LoadClientStructs(types);
        LoadAddons(types);
        LoadAgents(types);
        LoadInstances(types);

        CustomNodeTypes = _pluginInterface
            .GetOrCreateData("TypeMappedCustomNodes", () => new ConcurrentDictionary<nint, Type>());

        _loadedTcs.SetResult();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _pluginInterface.RelinquishData("TypeMappedCustomNodes");
        return Task.CompletedTask;
    }

    public Type GetAddonType(string addonName)
    {
        return AddonTypes != null && AddonTypes.TryGetValue(addonName, out var type) ? type : typeof(AtkUnitBase);
    }

    public Type GetAgentType(AgentId agentId)
    {
        return AgentTypes != null && AgentTypes.TryGetValue(agentId, out var type) ? type : typeof(AgentInterface);
    }

    public StructField[] GetStructFields(Type type)
    {
        if (structFields.TryGetValue(type, out var cachedFields))
            return cachedFields;

        var fields = new Dictionary<(int Offset, string Name, Type Type), StructField>();
        var baseSubobjects = new HashSet<(Type Type, int Offset)>();

        if (StructViews.TryGetValue(type, out var views))
        {
            CollectFields(views.Original, 0, views.Original);
            CollectFields(views.Expansion, 0, views.Expansion);
        }
        else
        {
            CollectFields(type, 0, type);
        }

        var result = fields.Values
            .OrderBy(field => field.Offset)
            .ThenBy(field => field.Field.Name, StringComparer.Ordinal)
            .ThenBy(field => field.Field.FieldType.FullName, StringComparer.Ordinal)
            .ToArray();

        structFields[type] = result;
        if (StructViews.TryGetValue(type, out views))
        {
            structFields[views.Original] = result;
            structFields[views.Expansion] = result;
        }

        return result;

        void CollectFields(Type currentType, int offset, Type parentType)
        {
            var parentOffset = 0;
            foreach (var attribute in currentType.GetCustomAttributes())
            {
                var attributeType = attribute.GetType();
                if (!attributeType.IsGenericType || attributeType.GetGenericTypeDefinition() != typeof(InheritsAttribute<>))
                    continue;

                var inheritedType = attributeType.GenericTypeArguments[0];
                var explicitOffset = (int)attributeType.GetProperty("ParentOffset")!.GetValue(attribute)!;
                if (explicitOffset != 0)
                    parentOffset = explicitOffset;

                baseSubobjects.Add((inheritedType, offset + parentOffset));
                CollectFields(inheritedType, offset + parentOffset, parentType);
                parentOffset += inheritedType.SizeOf();
            }

            foreach (var field in currentType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (field.IsLiteral || field.IsDefined(typeof(CompilerGeneratedAttribute), false))
                    continue;

                int fieldOffset;
                if (field.GetCustomAttribute<FieldOffsetAttribute>() is { } fieldOffsetAttribute)
                {
                    fieldOffset = fieldOffsetAttribute.Value;
                }
                else
                {
                    if (currentType.IsAutoLayout)
                        continue;

                    try
                    {
                        fieldOffset = (int)Marshal.OffsetOf(currentType, field.Name);
                    }
                    catch (ArgumentException)
                    {
                        continue;
                    }
                }

                var absoluteOffset = offset + fieldOffset;
                if (baseSubobjects.Contains((field.FieldType, absoluteOffset)))
                    continue;

                var original = currentType.Assembly == typeof(AddonAttribute).Assembly ? field : null;
                var expansion = currentType.Assembly == typeof(ExpansionInterop).Assembly ? field : null;
                var key = (absoluteOffset, field.Name, field.FieldType);
                if (fields.TryGetValue(key, out var existing))
                {
                    fields[key] = existing with
                    {
                        Original = existing.Original ?? original,
                        Expansion = existing.Expansion ?? expansion
                    };
                }
                else
                {
                    fields.Add(key, new StructField(field, absoluteOffset, parentType, original, expansion));
                }
            }
        }
    }

    public OrderedDictionary<int, (string, Type?)> GetTypeFields(Type type, string prefix = "", int offset = 0)
    {
        if (_offsetTypeStructs.TryGetValue(type, out var mapping))
            return mapping;

        _offsetTypeStructs[type] = mapping = [];

        LoadTypeMapping(mapping, prefix, offset, type);

        for (offset = 0; offset < type.SizeOf() - 8; offset += 8)
        {
            if (!mapping.ContainsKey(offset))
            {
                mapping[offset] = ($"+0x{offset:X}", null);
            }
        }

        return mapping;
    }

    private void LoadClientStructs(Type[] types)
    {
        CSTypes = types
            .Where(type => type.FullName != null && type.Namespace is { } typeNamespace &&
                (typeNamespace == "FFXIVClientStructs" || typeNamespace.StartsWith("FFXIVClientStructs.", StringComparison.Ordinal)))
            .ToImmutableSortedDictionary(
                type => type.FullName!,
                type => type);

        var views = ImmutableDictionary.CreateBuilder<Type, (Type Original, Type Expansion)>();
        foreach (var expansionType in types.Where(type => type.Assembly == typeof(ExpansionInterop).Assembly && type.IsStruct() && type.FullName != null))
        {
            if (expansionType.DeclaringType is { } declaringType && expansionType.Name == $"{declaringType.Name}VirtualTable")
                continue;

            var originalName = expansionType.FullName!.Replace("FFXIVClientStructs.Expansion.", "FFXIVClientStructs.", StringComparison.Ordinal);
            if (!CSTypes.TryGetValue(originalName, out var originalType) || originalType.Assembly != typeof(AddonAttribute).Assembly || !originalType.IsStruct())
                continue;

            views.Add(originalType, (originalType, expansionType));
            views.Add(expansionType, (originalType, expansionType));
        }

        StructViews = views.ToImmutable();
    }

    private void LoadAddons(Type[] types)
    {
        AddonTypes = types
            .Where(type => type.IsDefined(typeof(AddonAttribute), false) && !string.IsNullOrEmpty(type.Namespace))
            .SelectMany(type => type.GetCustomAttribute<AddonAttribute>()!.AddonIdentifiers, (type, addonName) => (type, addonName))
            .GroupBy(tuple => tuple.addonName)
            .Select(group => group.First())
            .ToImmutableSortedDictionary(
                tuple => tuple.addonName,
                tuple => tuple.type);
    }

    private void LoadAgents(Type[] types)
    {
        AgentTypes = types
            .Where(type => type.IsDefined(typeof(AgentAttribute), false) && !string.IsNullOrEmpty(type.Namespace))
            .Select(type => (type, agentId: type.GetCustomAttribute<AgentAttribute>()!.Id))
            .GroupBy(tuple => tuple.agentId)
            .Select(group => group.First())
            .ToImmutableSortedDictionary(
                tuple => tuple.agentId,
                tuple => tuple.type);
    }

    private void LoadInstances(Type[] types)
    {
        Instances = [.. types
            .Where(type => type.IsStruct() && type.GetMethod("Instance", BindingFlags.Static | BindingFlags.Public) is { } method && method.GetParameters().Length == 0 && method.ReturnType.IsPointer)
            .GroupBy(type => StructViews.TryGetValue(type, out var views) ? views.Original : type)
            .Select(group => group.First())];

        _logger.LogDebug("Loaded {count} instances", Instances.Length);
    }

    private static void LoadTypeMapping(OrderedDictionary<int, (string, Type?)> fields, string prefix, int offset, Type type)
    {
        foreach (var fieldInfo in type.GetFields(BindingFlags.Default | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            if (!fieldInfo.IsDefined(typeof(FieldOffsetAttribute), false))
                continue;

            if (fieldInfo.GetCustomAttribute<FieldOffsetAttribute>() is not { } fieldOffsetAttribute)
                continue;

            if (fieldInfo.IsAssembly
                && fieldInfo.IsDefined(typeof(FixedSizeArrayAttribute), false)
                && fieldInfo.FieldType.IsDefined(typeof(InlineArrayAttribute), false)
                && fieldInfo.GetCustomAttribute<FixedSizeArrayAttribute>() is FixedSizeArrayAttribute fixedSizeArrayAttribute
                && !fixedSizeArrayAttribute.IsString
                && !fixedSizeArrayAttribute.IsBitArray
                && fieldInfo.FieldType.GetCustomAttribute<InlineArrayAttribute>() is InlineArrayAttribute inlineArrayAttribute)
            {
                var innerType = fieldInfo.FieldType.GetFields(BindingFlags.Instance | BindingFlags.NonPublic)[0].FieldType;

                if (innerType.IsGenericType && innerType.Name.StartsWith("Pointer`1", StringComparison.Ordinal))
                    innerType = innerType.GenericTypeArguments[0].MakePointerType();

                if (innerType.IsStruct())
                {
                    for (var i = 0; i < inlineArrayAttribute.Length; i++)
                    {
                        LoadTypeMapping(fields, $"{prefix}{fieldInfo.Name[1..].FirstCharToUpper()}[{i}].", offset + fieldOffsetAttribute.Value + i * innerType.SizeOf(), innerType);
                    }
                }
                else
                {
                    for (var i = 0; i < inlineArrayAttribute.Length; i++)
                    {
                        fields[offset + fieldOffsetAttribute.Value + i * innerType.SizeOf()] = ($"{prefix}{fieldInfo.Name[1..].FirstCharToUpper()}[{i}]", innerType);
                    }
                }
            }
            else if (fieldInfo.FieldType.IsStruct())
            {
                LoadTypeMapping(fields, prefix + fieldInfo.Name + ".", offset + fieldOffsetAttribute.Value, fieldInfo.FieldType);
            }
            else if (!fields.ContainsKey(offset + fieldOffsetAttribute.Value))
            {
                fields[offset + fieldOffsetAttribute.Value] = (prefix + fieldInfo.Name, fieldInfo.FieldType);
            }
        }
    }
}

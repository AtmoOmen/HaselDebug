using System.Reflection;

namespace HaselDebug.Utils;

public readonly record struct StructField(
    FieldInfo Field,
    int Offset,
    Type ParentType,
    FieldInfo? Original,
    FieldInfo? Expansion);

using System.Reflection;
using AutoMapper;

namespace FashionCommerce.Application.Common.Mappings;

/// <summary>
/// A DTO implements this to declare "map me from TSource". MappingProfile
/// (below) scans the assembly and wires these up automatically, so adding
/// a new DTO never requires touching a manually-maintained Profile class.
/// </summary>
public interface IMapFrom<T>
{
    void Mapping(Profile profile) => profile.CreateMap(typeof(T), GetType());
}

public class MappingProfile : Profile
{
    public MappingProfile()
    {
        ApplyMappingsFromAssembly(Assembly.GetExecutingAssembly());
    }

    private void ApplyMappingsFromAssembly(Assembly assembly)
    {
        var mapFromType = typeof(IMapFrom<>);

        var mappingMethodName = nameof(IMapFrom<object>.Mapping);

        var types = assembly.GetExportedTypes()
            .Where(t => t.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == mapFromType))
            .ToList();

        foreach (var type in types)
        {
            var instance = Activator.CreateInstance(type);
            var methodInfo = type.GetMethod(mappingMethodName) ?? type.GetInterface("IMapFrom`1")?.GetMethod(mappingMethodName);
            methodInfo?.Invoke(instance, new object[] { this });
        }
    }
}

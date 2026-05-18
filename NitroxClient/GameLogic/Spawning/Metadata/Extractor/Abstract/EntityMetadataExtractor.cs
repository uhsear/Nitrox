using System.Diagnostics.CodeAnalysis;
using Nitrox.Model.Core;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;

namespace NitroxClient.GameLogic.Spawning.Metadata.Extractor.Abstract;

[SuppressMessage("Usage", "DIMA001:Dependency Injection container is used directly", Justification = "Abstract base class provides Resolve<T>() helper for subclasses that cannot individually use constructor injection")]
public abstract class EntityMetadataExtractor<I, O> : IEntityMetadataExtractor where O : EntityMetadata
{
    public abstract O Extract(I entity);

    public Optional<EntityMetadata> From(object o)
    {
        EntityMetadata result = Extract((I)o);

        return Optional.OfNullable(result);
    }

    protected T Resolve<T>() where T : class
    {
        return NitroxServiceLocator.Cache<T>.Value;
    }
}

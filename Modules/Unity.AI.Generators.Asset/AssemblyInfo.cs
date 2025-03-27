using System.ComponentModel;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Unity.AI.Sound")]
[assembly: InternalsVisibleTo("Unity.AI.Image")]
[assembly: InternalsVisibleTo("Unity.AI.Material")]
[assembly: InternalsVisibleTo("Unity.AI.Animate")]
[assembly: InternalsVisibleTo("Unity.AI.ModelSelector")]
[assembly: InternalsVisibleTo("Unity.AI.ModelTrainer")]
[assembly: InternalsVisibleTo("Unity.AI.Generators.UI")]
[assembly: InternalsVisibleTo("Unity.AI.Generators.Sdk")]
[assembly: InternalsVisibleTo("Unity.AI.Toolkit.Tests")]

// We need to add this to make the record type work in Unity with the init keyword
// The type System.Runtime.CompilerServices.IsExternalInit is defined in .NET 5 and later, which Unity does not support yet
namespace System.Runtime.CompilerServices
{
    [EditorBrowsable(EditorBrowsableState.Never)]
    internal class IsExternalInit { }
}

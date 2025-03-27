using System.ComponentModel;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Unity.AI.Image")]
[assembly: InternalsVisibleTo("Unity.AI.Sound")]
[assembly: InternalsVisibleTo("Unity.AI.Material")]
[assembly: InternalsVisibleTo("Unity.AI.ModelSelector")]
[assembly: InternalsVisibleTo("Unity.AI.ModelTrainer")]
[assembly: InternalsVisibleTo("Unity.AI.Animate")]
[assembly: InternalsVisibleTo("Unity.AI.Generators.Chat")]
[assembly: InternalsVisibleTo("Unity.AI.Generators.Asset")]
[assembly: InternalsVisibleTo("Unity.AI.Generators.UI")]
[assembly: InternalsVisibleTo("Unity.AI.Toolkit.Tests")]

// Needed to add this to avoid Rider underlining errors on Records with `init` properties.
namespace System.Runtime.CompilerServices
{
    [EditorBrowsable(EditorBrowsableState.Never)]
    class IsExternalInit{}
}

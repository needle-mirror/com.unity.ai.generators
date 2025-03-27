using System.ComponentModel;
using UnityEditor.UIElements;

[assembly: UxmlNamespacePrefix("Unity.AI.ModelTrainer.Components", "modelTrainer")]


// We need to add this to make the record type work in Unity with the init keyword
// The type System.Runtime.CompilerServices.IsExternalInit is defined in .NET 5 and later, which Unity does not support yet
namespace System.Runtime.CompilerServices
{
    [EditorBrowsable(EditorBrowsableState.Never)]
    internal class IsExternalInit { }
}

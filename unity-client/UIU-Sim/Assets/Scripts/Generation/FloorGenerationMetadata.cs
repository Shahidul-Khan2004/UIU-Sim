using UIU.Simulator.Building.Data;
using UnityEngine;

namespace UIU.Simulator.Building.Generation
{
    /// <summary>
    /// Records which data asset produced a Generated hierarchy.
    /// This component has no runtime behavior.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FloorGenerationMetadata : MonoBehaviour
    {
        [SerializeField] private FloorLayoutData layoutData;
        [SerializeField] private string generatorVersion;

        public FloorLayoutData LayoutData => layoutData;
        public string GeneratorVersion => generatorVersion;

        public void Initialize(FloorLayoutData sourceLayout, string version)
        {
            layoutData = sourceLayout;
            generatorVersion = version;
        }
    }
}

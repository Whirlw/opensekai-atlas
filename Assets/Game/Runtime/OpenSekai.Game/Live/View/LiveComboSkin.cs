using OpenSekai.MaterialPacks;

namespace Sekai
{
    public sealed class LiveComboSkin : LiveSpriteSkin
    {
        internal MaterialComboLayout Layout { get; }
        public LiveComboSkin(MaterialComboLayout layout = null) => Layout = layout;
    }
}

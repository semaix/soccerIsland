using UnityEditor;

namespace SoccerIsland.Field.EditorTools
{
    /// <summary>
    /// Resources/Art のドット絵を、にじまない設定で読み込む（ポイント補間・無圧縮・ミップマップなし）。
    /// キャラの色の塗り替えに使うので、読み取り可能にする。
    /// </summary>
    public class PixelArtImporter : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.Replace('\\', '/').Contains("Assets/Resources/Art/")) return;
            var ti = (TextureImporter)assetImporter;
            ti.textureType = TextureImporterType.Default;
            ti.filterMode = UnityEngine.FilterMode.Point;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.mipmapEnabled = false;
            ti.isReadable = true;
            ti.npotScale = TextureImporterNPOTScale.None;
            ti.alphaIsTransparency = true;
        }
    }
}

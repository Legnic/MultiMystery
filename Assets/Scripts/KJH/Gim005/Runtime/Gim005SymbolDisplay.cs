using UnityEngine;

namespace Gim005
{
    // 문양 하나를 보여 주는 명판 (덤불 앞뒤 명판, 2층 버튼 표면 공용).
    //  - quad : 그림이 나올 판 (지금은 단색 명판)
    //  - text : 임시 숫자
    // Gim005SymbolData의 sprite가 비어 있으면 숫자를 보이고, 채워져 있으면 quad에 그림을 입히고 숫자를 숨긴다.
    // 머티리얼을 복제하지 않고 MaterialPropertyBlock으로 그림만 바꾸므로, 명판이 많아도 머티리얼 에셋은 하나다.
    public class Gim005SymbolDisplay : MonoBehaviour
    {
        [SerializeField] Renderer quad;
        [SerializeField] TextMesh text;

        static readonly int BaseMap = Shader.PropertyToID("_BaseMap");
        static readonly int BaseMapST = Shader.PropertyToID("_BaseMap_ST");
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly int MainTex = Shader.PropertyToID("_MainTex");

        MaterialPropertyBlock block;
        int shown = int.MinValue;
        Sprite shownSprite;

        // symbol < 0 이면 빈 명판
        public void Show(int symbol, Gim005SymbolData data)
        {
            var entry = data != null ? data.Get(symbol) : null;
            var sprite = entry != null ? entry.sprite : null;
            if (symbol == shown && sprite == shownSprite) return;
            shown = symbol;
            shownSprite = sprite;

            if (text != null)
            {
                text.gameObject.SetActive(symbol >= 0 && sprite == null);
                text.text = symbol >= 0 ? (entry != null ? entry.label : (symbol + 1).ToString()) : "";
            }

            if (quad == null) return;
            if (block == null) block = new MaterialPropertyBlock();
            quad.GetPropertyBlock(block);
            if (sprite != null)
            {
                // 아틀라스에 들어 있는 스프라이트도 그 부분만 보이도록 UV 범위를 맞춘다
                var tex = sprite.texture;
                var r = sprite.textureRect;
                var st = new Vector4(r.width / tex.width, r.height / tex.height, r.x / tex.width, r.y / tex.height);
                block.SetTexture(BaseMap, tex);
                block.SetTexture(MainTex, tex);
                block.SetVector(BaseMapST, st);
                block.SetColor(BaseColor, Color.white);
                quad.SetPropertyBlock(block);
            }
            else
            {
                quad.SetPropertyBlock(null);   // 머티리얼 원래 색(명판)으로
            }
        }
    }
}

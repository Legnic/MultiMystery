using System;
using UnityEngine;

namespace PotionCraft
{
    // 이름·색·손에 드는 모양(프리팹)을 한곳에 모은 표. 외형 교체(모델링)는 여기 프리팹만 바꾸면 된다.
    [CreateAssetMenu(menuName = "KJH/PotionCraft/Catalog", fileName = "PotionCatalog")]
    public class PotionCatalog : ScriptableObject
    {
        [Serializable] public class MaterialInfo { public MaterialId id; public string displayName; public Color color = Color.white; public GameObject prefab; }
        [Serializable] public class WaterInfo { public WaterId id; public string displayName; public Color color = Color.white; public GameObject prefab; }
        [Serializable] public class PowderInfo { public PowderColor id; public string displayName; public Color color = Color.white; public GameObject prefab; }

        public MaterialInfo[] materials;
        public WaterInfo[] waters;
        public PowderInfo[] powders;

        [Header("덩이 (결과 색과 상관없이 항상 같은 이름·같은 모양)")]
        public string lumpName = "재료 덩이";
        public Color lumpColor = new Color(0.45f, 0.45f, 0.45f);
        public GameObject lumpPrefab;
        [Header("가루 (종이 위 가루, 색으로 구분)")]
        public GameObject powderPrefab;

        [Header("연출 (비어 있어도 동작)")]
        public Material fxMaterial;
        public AudioClip rejectClip;
        public AudioClip explodeClip;
        public AudioClip successClip;

        public string NameOf(int code)
        {
            int sub = Item.SubOf(code);
            switch (Item.KindOf(code))
            {
                case ItemKind.Material: { var m = Find(materials, sub); return m != null ? m.displayName : "재료"; }
                case ItemKind.Water: { var w = Find(waters, sub); return w != null ? w.displayName : "물"; }
                case ItemKind.Lump: return lumpName; // 결과 색을 절대 드러내지 않는다
                case ItemKind.Powder: { var p = FindPowder(sub); return p != null ? p.displayName : "가루"; }
            }
            return string.Empty;
        }

        public Color ColorOf(int code)
        {
            int sub = Item.SubOf(code);
            switch (Item.KindOf(code))
            {
                case ItemKind.Material: { var m = Find(materials, sub); return m != null ? m.color : Color.white; }
                case ItemKind.Water: { var w = Find(waters, sub); return w != null ? w.color : Color.white; }
                case ItemKind.Lump: return lumpColor;
                case ItemKind.Powder: return PowderColorOf((PowderColor)sub);
            }
            return Color.white;
        }

        public Color PowderColorOf(PowderColor c) { var p = FindPowder((int)c); return p != null ? p.color : Color.white; }
        public string PowderNameOf(PowderColor c) { var p = FindPowder((int)c); return p != null ? p.displayName : c.ToString(); }

        public GameObject VisualPrefab(int code)
        {
            int sub = Item.SubOf(code);
            switch (Item.KindOf(code))
            {
                case ItemKind.Material: { var m = Find(materials, sub); return m != null ? m.prefab : null; }
                case ItemKind.Water: { var w = Find(waters, sub); return w != null ? w.prefab : null; }
                case ItemKind.Lump: return lumpPrefab;
                case ItemKind.Powder: { var p = FindPowder(sub); return p != null && p.prefab != null ? p.prefab : powderPrefab; }
            }
            return null;
        }

        MaterialInfo Find(MaterialInfo[] a, int sub) { if (a != null) foreach (var x in a) if (x != null && (int)x.id == sub) return x; return null; }
        WaterInfo Find(WaterInfo[] a, int sub) { if (a != null) foreach (var x in a) if (x != null && (int)x.id == sub) return x; return null; }
        PowderInfo FindPowder(int sub) { if (powders != null) foreach (var x in powders) if (x != null && (int)x.id == sub) return x; return null; }
    }
}

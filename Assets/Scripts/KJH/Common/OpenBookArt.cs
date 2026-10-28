using UnityEngine;

// 화면에 펼쳐 읽는 책(GIM-002 놀이책, GIM-004 기록일지)이 같이 쓰는 "펼친 책" 그림과 쪽 위치.
// 그림: Assets/Images/KJH/Resources/KJH_OpenBook.png (1671×941). 쪽 위치는 그림의 종이 부분을 재서 정한 비율이다.
// 그림을 바꾸면 아래 비율(LeftPageUV/RightPageUV)만 다시 맞추면 된다.
public static class OpenBookArt
{
    const string ResourcePath = "KJH_OpenBook";
    const float Aspect = 1671f / 941f;

    // 글·사진을 놓을 안전한 영역 (그림 기준 0~1 비율: x, y, 너비, 높이). 종이 가장자리·가운데 접힌 곳에서 조금 띄웠다.
    static readonly Rect LeftPageUV = new Rect(0.165f, 0.135f, 0.295f, 0.685f);
    static readonly Rect RightPageUV = new Rect(0.545f, 0.135f, 0.295f, 0.685f);

    static Texture2D cached;

    // override가 있으면 그것을, 없으면 Resources의 기본 그림을 쓴다. 그림이 없으면 단색 종이로 대체.
    public static Texture2D Get(Texture2D overrideTexture)
    {
        if (overrideTexture != null) return overrideTexture;
        if (cached == null) cached = Resources.Load<Texture2D>(ResourcePath);
        if (cached == null)
        {
            cached = new Texture2D(1, 1);
            cached.SetPixel(0, 0, new Color(0.94f, 0.9f, 0.8f));
            cached.Apply();
        }
        return cached;
    }

    // 화면(가상 해상도 w×h) 가운데에 그림 비율을 유지하며 가로 90%·세로 86% 안에 맞춘 책 영역
    public static Rect Fit(float w, float h)
    {
        float bw = Mathf.Min(w * 0.9f, h * 0.86f * Aspect);
        float bh = bw / Aspect;
        return new Rect((w - bw) * 0.5f, (h - bh) * 0.5f, bw, bh);
    }

    public static Rect LeftPage(Rect book) { return Map(book, LeftPageUV); }
    public static Rect RightPage(Rect book) { return Map(book, RightPageUV); }

    static Rect Map(Rect book, Rect uv)
    {
        return new Rect(book.x + book.width * uv.x, book.y + book.height * uv.y, book.width * uv.width, book.height * uv.height);
    }
}

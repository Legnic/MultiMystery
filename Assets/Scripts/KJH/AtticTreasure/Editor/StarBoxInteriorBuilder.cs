using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace AtticTreasure.EditorTools
{
    // 별 상자 안쪽 메시(AtticTreasure_StarBox_Interior)를 다시 만든다.
    // 몸체 모델의 윗면은 잘라 낸 자리라 울퉁불퉁해서, 안쪽 테두리가 몸체 바깥 벽까지 덮어야 이음새가 안 보인다.
    // 구성: 바닥 + 안쪽 벽 4개 + 안쪽 모서리 깎임 + 윗면 테두리 + 바깥 모서리 깎임 + 바깥 덧댐(울퉁불퉁한 윗선을 가림).
    // 값은 메시 로컬 좌표(Interior 오브젝트: 위치 y 0.144, 크기 0.488)다.
    public static class StarBoxInteriorBuilder
    {
        const string MeshPath = "Assets/Prefabs/KJH/AtticTreasure/Meshes/AtticTreasure_StarBox_Interior.asset";

        const float FloorY = -0.2386f;
        const float WallX = 0.52f, WallZ = 0.33f;     // 안쪽 벽
        const float WallTopY = 0.169f;                // 안쪽 벽 끝(여기서 깎인 면이 시작)
        const float TopY = 0.1752f;                   // 테두리 윗면 (몸체 윗면 최고점보다 살짝 위)
        const float InnerBevel = 0.006f;              // 안쪽 모서리 깎임 폭
        const float OuterTopX = 0.5765f, OuterTopZ = 0.3955f; // 윗면 바깥 끝
        const float SkirtX = 0.5815f, SkirtZ = 0.4005f;     // 바깥 덧댐 (몸체 바깥 벽 바로 안쪽: 닫혔을 때 바깥으로 띠가 안 보이게)
        const float SkirtTopY = 0.1712f, SkirtBottomY = 0.1434f;
        const float UvScale = 2f;                     // 기존 메시와 같은 나뭇결 크기

        [MenuItem("Tools/KJH/AtticTreasure/Rebuild Star Box Interior")]
        public static void Rebuild()
        {
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
            if (mesh == null) { Debug.LogError("[StarBoxInterior] 메시가 없다: " + MeshPath); return; }

            var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();

            // 바닥
            Quad(v, n, uv, t, new Vector3(-WallX, FloorY, -WallZ), new Vector3(WallX, FloorY, -WallZ),
                 new Vector3(WallX, FloorY, WallZ), new Vector3(-WallX, FloorY, WallZ), Vector3.up);

            // 네 방향을 같은 규칙으로: (안쪽 벽 반폭, 바깥 반폭 비율)을 x/z로 바꿔 끼운다.
            Ring(v, n, uv, t, WallX, WallZ, FloorY, WallX, WallZ, WallTopY, true);                                   // 안쪽 벽
            Ring(v, n, uv, t, WallX, WallZ, WallTopY, WallX + InnerBevel, WallZ + InnerBevel, TopY, true);          // 안쪽 깎임
            Ring(v, n, uv, t, WallX + InnerBevel, WallZ + InnerBevel, TopY, OuterTopX, OuterTopZ, TopY, true);      // 윗면
            Ring(v, n, uv, t, OuterTopX, OuterTopZ, TopY, SkirtX, SkirtZ, SkirtTopY, false);                         // 바깥 깎임
            Ring(v, n, uv, t, SkirtX, SkirtZ, SkirtTopY, SkirtX, SkirtZ, SkirtBottomY, false);                        // 바깥 덧댐

            mesh.Clear();
            mesh.SetVertices(v); mesh.SetNormals(n); mesh.SetUVs(0, uv); mesh.SetTriangles(t, 0);
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            AssetDatabase.SaveAssets();
            Debug.Log("[StarBoxInterior] 다시 만듦: 정점 " + v.Count + ", 삼각형 " + t.Count / 3);
        }

        // 사각 고리 한 단: 아래 테(ax, az, ay) → 위 테(bx, bz, by). inward = 면이 상자 안쪽(또는 위)을 본다.
        static void Ring(List<Vector3> v, List<Vector3> n, List<Vector2> uv, List<int> t,
                         float ax, float az, float ay, float bx, float bz, float by, bool inward)
        {
            // 앞(-z), 오른쪽(+x), 뒤(+z), 왼쪽(-x) 순서
            for (int side = 0; side < 4; side++)
            {
                Vector3 a0, a1, b0, b1, outDir;
                switch (side)
                {
                    case 0: a0 = new Vector3(-ax, ay, -az); a1 = new Vector3(ax, ay, -az); b0 = new Vector3(-bx, by, -bz); b1 = new Vector3(bx, by, -bz); outDir = Vector3.back; break;
                    case 1: a0 = new Vector3(ax, ay, -az); a1 = new Vector3(ax, ay, az); b0 = new Vector3(bx, by, -bz); b1 = new Vector3(bx, by, bz); outDir = Vector3.right; break;
                    case 2: a0 = new Vector3(ax, ay, az); a1 = new Vector3(-ax, ay, az); b0 = new Vector3(bx, by, bz); b1 = new Vector3(-bx, by, bz); outDir = Vector3.forward; break;
                    default: a0 = new Vector3(-ax, ay, az); a1 = new Vector3(-ax, ay, -az); b0 = new Vector3(-bx, by, bz); b1 = new Vector3(-bx, by, -bz); outDir = Vector3.left; break;
                }
                // 면의 실제 기울기에서 법선을 구하고, 원하는 쪽(안쪽/바깥쪽 + 위)으로 맞춘다.
                Vector3 normal = Vector3.Cross(a1 - a0, b0 - a0).normalized;
                if (normal == Vector3.zero) normal = Vector3.up;
                Vector3 want = inward ? -outDir + Vector3.up * 0.001f : outDir + Vector3.up * 0.001f;
                if (Mathf.Approximately(ay, by)) want = Vector3.up;
                if (Vector3.Dot(normal, want) < 0f) normal = -normal;
                Quad(v, n, uv, t, a0, a1, b1, b0, normal);
            }
        }

        // 네 점 사각형. 감김 방향은 법선 쪽에서 보이도록 자동으로 맞춘다. 나뭇결은 법선의 주축 평면에 투영.
        static void Quad(List<Vector3> v, List<Vector3> n, List<Vector2> uv, List<int> t,
                         Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Vector3 normal)
        {
            int i = v.Count;
            var ps = new[] { p0, p1, p2, p3 };
            foreach (var p in ps)
            {
                v.Add(p); n.Add(normal);
                float ax = Mathf.Abs(normal.x), ay = Mathf.Abs(normal.y), az = Mathf.Abs(normal.z);
                Vector2 u = ay >= ax && ay >= az ? new Vector2(p.x, p.z) : ax >= az ? new Vector2(p.z, p.y) : new Vector2(p.x, p.y);
                uv.Add(u * UvScale);
            }
            bool front = Vector3.Dot(Vector3.Cross(p1 - p0, p2 - p0), normal) > 0f;
            if (front) t.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
            else t.AddRange(new[] { i, i + 2, i + 1, i, i + 3, i + 2 });
        }
    }
}

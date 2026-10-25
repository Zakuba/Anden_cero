using System;
using System.IO;
using UnityEngine;

public static class AvatarStorage
{
    const int Size = 128;

    public static byte[] LoadPng()
    {
        var data = PersistenceManager.LoadData();
        if (string.IsNullOrEmpty(data.avatarBase64)) return null;
        return Convert.FromBase64String(data.avatarBase64);
    }

    public static bool SetFromFile(string sourcePath)
    {
        var tex = new Texture2D(2, 2);
        if (!tex.LoadImage(File.ReadAllBytes(sourcePath)))
        {
            UnityEngine.Object.Destroy(tex);
            return false;
        }

        var small = Resize(tex, Size, Size);
        MakeCircle(small);
        byte[] png = small.EncodeToPNG();
        UnityEngine.Object.Destroy(small);

        var data = PersistenceManager.LoadData();
        data.avatarBase64 = Convert.ToBase64String(png);
        PersistenceManager.SaveData(data);
        return true;
    }

    static Texture2D Resize(Texture2D src, int w, int h)
    {
        var rt = RenderTexture.GetTemporary(w, h);

        float aspect = (float)src.width / src.height;
        Vector2 scale = aspect > 1f ? new Vector2(1f / aspect, 1f) : new Vector2(1f, aspect);
        Vector2 offset = (Vector2.one - scale) / 2f;
        Graphics.Blit(src, rt, scale, offset);

        var prev = RenderTexture.active;
        RenderTexture.active = rt;
        var res = new Texture2D(w, h, TextureFormat.RGBA32, false);
        res.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        res.Apply();
        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);
        UnityEngine.Object.Destroy(src);
        return res;
    }
    static void MakeCircle(Texture2D t)
    {
        int n = t.width;                 // es cuadrada (128x128)
        float r = n / 2f;
        Color32[] px = t.GetPixels32();

        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                float dx = x + 0.5f - r;
                float dy = y + 0.5f - r;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float alpha = Mathf.Clamp01(r - dist);   // borde suavizado de 1 píxel
                int i = y * n + x;
                px[i].a = (byte)(px[i].a * alpha);
            }
        }

        t.SetPixels32(px);
        t.Apply();
    }
}
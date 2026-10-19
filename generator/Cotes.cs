// Cotes de Natural Earth 1:50m (domaine public, naturalearthdata.com : aucune autorisation ni mention requise),
// seulement les zones qui bordent les cartes de Poseidon (ouest et nord-est de l'Amerique du Nord, Iberie et Maroc,
// golfe de Guinee, Moyen-Orient),
// decoupees et simplifiees a 0,02 degre. Anneaux de points (longitude, latitude) x 100, en entiers 16 bits, compresses.
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

public static class Cotes
{
    static List<double[][]> rings;

    public static List<double[][]> Rings()
    {
        if (rings != null) return rings;
        rings = new List<double[][]>();
        using (DeflateStream d = new DeflateStream(new MemoryStream(Convert.FromBase64String(Data)), CompressionMode.Decompress))
        using (BinaryReader r = new BinaryReader(d))
        {
            int n = r.ReadInt32();
            for (int i = 0; i < n; i++)
            {
                double[][] ring = new double[r.ReadInt32()][];
                for (int k = 0; k < ring.Length; k++) { double lon = r.ReadInt16() / 100.0; ring[k] = new[] { lon, r.ReadInt16() / 100.0 }; }
                rings.Add(ring);
            }
        }
        return rings;
    }

    const string Data =
        "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";
}

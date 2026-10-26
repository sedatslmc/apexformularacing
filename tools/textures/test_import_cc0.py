#!/usr/bin/env python3
"""Self-test of import_cc0.py with a synthetic Poly-Haven-style set (DirectX normal, roughness, AO)."""
import json
import os
import sys
import tempfile
import unittest

import numpy as np
from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import import_cc0  # noqa: E402


class ImportCC0Test(unittest.TestCase):
    def test_converts_a_set_for_unity(self):
        with tempfile.TemporaryDirectory() as tmp:
            src = os.path.join(tmp, "src")
            os.makedirs(src)
            rng = np.random.default_rng(1)
            s = 64
            Image.fromarray((rng.random((s, s, 3)) * 80 + 60).astype(np.uint8)).save(os.path.join(src, "x_diff_2k.jpg"))
            n = np.zeros((s, s, 3), np.uint8)
            n[..., 0], n[..., 1], n[..., 2] = 128, 100, 255
            Image.fromarray(n).save(os.path.join(src, "x_nor_dx_2k.png"))
            Image.fromarray(np.full((s, s), 179, np.uint8)).save(os.path.join(src, "x_rough_2k.png"))   # roughness 0.7
            Image.fromarray(np.full((s, s), 200, np.uint8)).save(os.path.join(src, "x_ao_2k.png"))
            out = os.path.join(tmp, "out")
            import_cc0.main([src, "--name", "t", "--metres", "3", "--paint-base", "--out-root", out])
            d = os.path.join(out, "t")
            meta = json.load(open(os.path.join(d, "source.json")))
            self.assertEqual(meta["metres"], 3)
            self.assertAlmostEqual(meta["meanSmoothness"], 1 - 179 / 255, places=3)
            self.assertGreater(meta["meanAlbedoLinear"], 0.04)   # sRGB 60..140 -> linear about 0.05..0.26
            self.assertLess(meta["meanAlbedoLinear"], 0.2)
            for f in ("BaseColor.jpg", "PaintBase.jpg", "Normal.png", "MetallicSmoothness.png", "Occlusion.jpg"):
                self.assertIn(f, meta["files"])
            nrm = np.asarray(Image.open(os.path.join(d, "Normal.png")))
            self.assertEqual(nrm[0, 0, 1], 155, "DirectX green flipped to OpenGL")
            ms = np.asarray(Image.open(os.path.join(d, "MetallicSmoothness.png")))
            self.assertEqual(ms.shape[2], 4)
            self.assertEqual(ms[..., 0].max(), 0, "not metallic")
            self.assertAlmostEqual(ms[..., 3].mean() / 255, 1 - 179 / 255, places=2)
            paint = np.asarray(Image.open(os.path.join(d, "PaintBase.jpg")).convert("L"))
            self.assertAlmostEqual(paint.mean() / 255, 0.82, places=2)


if __name__ == "__main__":
    unittest.main()

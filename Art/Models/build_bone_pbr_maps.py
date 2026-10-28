"""Create tileable technical PBR maps for the anatomical skeleton.

The albedo is the image-generated Bone_BaseColor.png beside these outputs.
This script creates independent, mathematically periodic micro-height noise and
derives tangent-space normal, roughness, and Unity metallic/smoothness maps.
Run with Blender's Python. No directional lighting or AO is baked into maps.
"""

from pathlib import Path

import bpy
import numpy as np


TEXTURES = Path(__file__).resolve().parents[2] / "Assets" / "Art" / "Textures" / "Bone"
SIZE = 1024


def periodic_noise(seed: int, blur_pixels: float) -> np.ndarray:
    rng = np.random.default_rng(seed)
    white = rng.standard_normal((SIZE, SIZE))
    kx = np.fft.rfftfreq(SIZE)[None, :]
    ky = np.fft.fftfreq(SIZE)[:, None]
    spectrum = np.fft.rfft2(white) * np.exp(-2 * np.pi**2 * blur_pixels**2 * (kx**2 + ky**2))
    result = np.fft.irfft2(spectrum, s=(SIZE, SIZE)).real
    result -= result.mean()
    result /= max(result.std(), 1e-8)
    return result.astype(np.float32)


def save_png(name: str, rgb: np.ndarray, alpha: np.ndarray | None = None) -> None:
    existing = bpy.data.images.get(name)
    if existing:
        bpy.data.images.remove(existing)
    image = bpy.data.images.new(name, width=SIZE, height=SIZE, alpha=True)
    pixels = np.empty((SIZE, SIZE, 4), dtype=np.float32)
    pixels[:, :, :3] = np.clip(rgb, 0, 1)
    pixels[:, :, 3] = 1 if alpha is None else np.clip(alpha, 0, 1)
    image.pixels.foreach_set(pixels.ravel())
    image.file_format = "PNG"
    image.filepath_raw = str(TEXTURES / f"{name}.png")
    image.save()
    bpy.data.images.remove(image)


def main() -> None:
    if not (TEXTURES / "Bone_BaseColor.png").is_file():
        raise FileNotFoundError("Copy the generated Bone_BaseColor.png into the Bone texture folder first")
    fine = periodic_noise(4731, 1.2)
    broad = periodic_noise(7759, 9.0)
    height = .10 * fine + .08 * broad
    gx = (np.roll(height, -1, axis=1) - np.roll(height, 1, axis=1)) * .5
    gy = (np.roll(height, -1, axis=0) - np.roll(height, 1, axis=0)) * .5
    nx, ny, nz = -.75 * gx, -.75 * gy, np.ones_like(gx)
    magnitude = np.sqrt(nx * nx + ny * ny + nz * nz)
    normal = np.stack((nx / magnitude * .5 + .5,
                       ny / magnitude * .5 + .5,
                       nz / magnitude * .5 + .5), axis=2)
    roughness = np.clip(.79 + .035 * broad + .025 * fine, .68, .89)
    rough_rgb = np.repeat(roughness[:, :, None], 3, axis=2)
    metallic = np.zeros_like(normal)
    save_png("Bone_Normal", normal)
    save_png("Bone_Roughness", rough_rgb)
    save_png("Bone_MetallicSmoothness", metallic, 1 - roughness)
    print(f"Generated tileable {SIZE}x{SIZE} bone normal, roughness, and Unity mask maps in {TEXTURES}")


if __name__ == "__main__":
    main()

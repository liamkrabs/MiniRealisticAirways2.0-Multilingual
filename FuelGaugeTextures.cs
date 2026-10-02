using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace MiniRealisticAirways;

public static class FuelGaugeTextures
{
	public static Rect rect_;

	public static List<Texture2D> fuelTextures_;

	private static List<Sprite> fuelSprites_;

	public const int SIZE = 35;

	public const int REFRESH_GRADIENT = 100;
	private const string FuelIconResource = "MiniRealisticAirways.assets.fuel-icon.png";

	private static byte[] LoadIconAlpha()
	{
		Texture2D source = null;
		try
		{
			using Stream stream = typeof(FuelGaugeTextures).Assembly.GetManifestResourceStream(FuelIconResource);
			if (stream == null) throw new FileNotFoundException("Embedded fuel icon is missing.");
			using MemoryStream bytes = new MemoryStream();
			stream.CopyTo(bytes);
			source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
			if (!ImageConversion.LoadImage(source, bytes.ToArray(), false)) throw new InvalidDataException("Fuel icon PNG could not be decoded.");
			source.wrapMode = TextureWrapMode.Clamp;
			byte[] mask = new byte[SIZE * SIZE];
			for (int y = 0; y < SIZE; y++)
			for (int x = 0; x < SIZE; x++)
				mask[y * SIZE + x] = (byte)Mathf.RoundToInt(source.GetPixelBilinear((x + 0.5f) / SIZE, (y + 0.5f) / SIZE).a * 255f);
			FuelIconRasterizer.CreateFrame(mask, SIZE, SIZE, 100); // Validate a nonempty silhouette.
			Plugin.LogDebug($"Loaded embedded fuel-can icon ({source.width}x{source.height}); display canvas={SIZE}x{SIZE}.");
			return mask;
		}
		catch (Exception ex)
		{
			Plugin.Log?.LogWarning("Fuel icon loading failed; retaining legacy droplet: " + ex.Message);
			return null;
		}
		finally { if (source != null) UnityEngine.Object.Destroy(source); }
	}

	private static Texture2D DrawFuelIcon(byte[] alpha, int percent)
	{
		Texture2D texture = new Texture2D(SIZE, SIZE, TextureFormat.RGBA32, false);
		texture.name = "MiniRealisticAirways FuelCan " + percent;
		texture.filterMode = FilterMode.Bilinear;
		texture.wrapMode = TextureWrapMode.Clamp;
		texture.LoadRawTextureData(FuelIconRasterizer.CreateFrame(alpha, SIZE, SIZE, percent));
		texture.Apply(false);
		return texture;
	}

	private static Texture2D DrawDroplet(int step)
	{
		int filledRows = step * SIZE / REFRESH_GRADIENT;
		Texture2D texture = new Texture2D(SIZE, SIZE);
		Color32[] pixels = new Color32[SIZE * SIZE];
		for (int y = 0; y < SIZE; y++)
		for (int x = 0; x < SIZE; x++)
		{
			bool inside = y < 14
				? Math.Sqrt((x - 17) * (x - 17) + (y - 17) * (y - 17)) < 17.0
				: Math.Sin((double)((float)x / 12f) + 3.25) + 0.05 < Math.Sin((double)(float)y / 17.5 + 2.4);
			pixels[y * SIZE + x] = inside ? (Color32)(y < filledRows ? Color.white : Color.gray) : (Color32)Color.clear;
		}
		texture.SetPixels32(pixels);
		texture.Apply();
		return texture;
	}

	public static void PreLoadTextures()
	{
		if (fuelTextures_ != null && fuelTextures_.Count == 101 && fuelSprites_ != null && fuelSprites_.Count == 101)
		{
			return;
		}
		DestroyTextures();
		Plugin.LogDebug("Pre-rendered fuel gauge textures.");
		fuelTextures_ = new List<Texture2D>(101);
		byte[] iconAlpha = LoadIconAlpha();
		for (int i = 0; i <= REFRESH_GRADIENT; i++)
		{
			fuelTextures_.Add(iconAlpha == null ? DrawDroplet(i) : DrawFuelIcon(iconAlpha, i));
		}
		rect_ = new Rect(0f, 0f, 35f, 35f);
		fuelSprites_ = new List<Sprite>(101);
		for (int j = 0; j <= REFRESH_GRADIENT; j++)
		{
			fuelSprites_.Add(null);
		}
	}

	public static Sprite GetSprite(int percent)
	{
		if (fuelTextures_ == null || fuelSprites_ == null || percent < 0 || percent >= fuelTextures_.Count)
		{
			return null;
		}
		Sprite sprite = fuelSprites_[percent];
		if (sprite == null && fuelTextures_[percent] != null)
		{
			sprite = Sprite.Create(fuelTextures_[percent], rect_, Vector2.zero);
			sprite.name = "MiniRealisticAirways Fuel " + percent;
			sprite.hideFlags = HideFlags.HideAndDontSave;
			fuelSprites_[percent] = sprite;
		}
		return sprite;
	}

	public static void DestroyTextures()
	{
		if (fuelTextures_ == null && fuelSprites_ == null)
		{
			return;
		}
		Plugin.LogDebug("Fuel gauge textures destroyed.");
		if (fuelSprites_ != null)
		{
			for (int i = 0; i < fuelSprites_.Count; i++)
			{
				if (fuelSprites_[i] != null)
				{
					UnityEngine.Object.Destroy(fuelSprites_[i]);
				}
			}
			fuelSprites_.Clear();
			fuelSprites_ = null;
		}
		if (fuelTextures_ == null)
		{
			return;
		}
		for (int i = 0; i < fuelTextures_.Count; i++)
		{
			if ((bool)fuelTextures_[i])
			{
				UnityEngine.Object.Destroy(fuelTextures_[i]);
			}
		}
		fuelTextures_.Clear();
		fuelTextures_ = null;
	}
}

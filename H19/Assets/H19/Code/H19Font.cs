using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class H19Font : MonoBehaviour
{
	public Texture2D[] fontCharArray = new Texture2D[256];

	void Start()
	{
		LoadROM();
	}

	void LoadROM()
	{
		string h19fontRom = System.IO.Path.Combine(Application.streamingAssetsPath, "2716_444-29_H19FONT.BIN");
		byte[] rom = LoadROMFile(h19fontRom);
		if (rom != null)
		{
			// convert to Texture2D
			for (int i = 0; i < 128; i++)
			{
				fontCharArray[i] = new Texture2D(8, 10);
				fontCharArray[i].filterMode = FilterMode.Point;
				fontCharArray[i + 128] = new Texture2D(8, 10);
				fontCharArray[i + 128].filterMode = FilterMode.Point;
				int y = 0;
				int x = 0;
				int n = i * 16;
				for (int j = 0; j < 10; j++)
				{
					byte c = rom[n + j];
					for (int k = 0; k < 8; k++)
					{
						int bit = (128 >> k);
						if ((c & bit) != 0)
						{
							fontCharArray[i].SetPixel(x + k, y + j, Color.green);
							fontCharArray[i + 128].SetPixel(x + k, y + j, Color.black);
						}
						else
						{
							fontCharArray[i].SetPixel(x + k, y + j, Color.black);
							fontCharArray[i + 128].SetPixel(x + k, y + j, Color.green);
						}
					}
				}
				fontCharArray[i + 128].Apply();
				fontCharArray[i].Apply();
			}
		}
	}

	byte[] LoadROMFile(string file)
	{
		if (System.IO.File.Exists (file)) {
			byte[] contents = System.IO.File.ReadAllBytes(file);
			return contents;
		}
		return null;
	}
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MCZLoadDump : MonoBehaviour
{
    public string fileName;
    public static string globalFileName;

    void Start()
    {
        globalFileName = fileName;
    }

    void Update()
    {
        
    }

    public static byte[] GetDumpBinary(string dumpFile, bool saveBinary = false)
    {
        Debug.Log("GetDumpBinary() dumpFile=" + dumpFile);

        List<byte> binary = new List<byte>();
        if (System.IO.File.Exists(dumpFile))
        {
            string[] dumpFileArray = System.IO.File.ReadAllLines(dumpFile);
            for (int i = 0; i < dumpFileArray.Length; i++)
            {
                int n = 0;
                string s = dumpFileArray[i];
                if (!string.IsNullOrEmpty(s) && s.Length > 16)
                {
                    for (int j = 0; j < 16; j++)
                    {
                        if (j > 0 && (j % 4) == 0)
                        {
                            n += 1;
                        }

                        string h = s.Substring(n, 2);

                        //Debug.Log("n=" + n.ToString() + " " + h);

                        byte b = (byte)int.Parse(h, System.Globalization.NumberStyles.HexNumber);
                        binary.Add(b);

                        n += 3;
                    }
                }
            }

            if (saveBinary)
            {
                if (binary.Count > 0)
                {
                    string desktopPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Desktop);
                    string fileName = globalFileName + ".BIN";
                    string outputFile = System.IO.Path.Combine(desktopPath, fileName);
                    System.IO.File.WriteAllBytes(outputFile, binary.ToArray());
                }
            }
        }

        return binary.ToArray();
    }
}

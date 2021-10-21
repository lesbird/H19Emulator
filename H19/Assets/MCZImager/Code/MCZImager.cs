using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MCZImager : MonoBehaviour
{
    [HideInInspector]
    public int bytesPerSector;
    public int sectorsPerTrack;
    public int tracks;
    public bool modeImg;
    [HideInInspector]
    public bool modeRaw;    // for debugging - use MCZRAW.S on MCZ computer
    public GameObject mczCanvas;
    public GameObject imagerPanel;
    public GameObject warningPanelRoot;
    public GameObject safeModePanel;
    public GameObject loaderPanel;
    public UnityEngine.UI.Slider progressSlider;
    public UnityEngine.UI.Text progressText;
    public UnityEngine.UI.Toggle safeModeToggle;

    public string loaderStartAdr;

    private bool inSendCoroutine;
    private byte sndCmd;

    private static System.DateTime startTime;

    public static bool readImage;
    public static bool sendImage;
    public static bool sendReady;

    private static byte[] diskImageBuffer;
    private static int byteIndex;

    public static MCZImager Instance;

    void Awake()
    {
        Instance = this;

        bytesPerSector = 136;
        if (modeRaw)
        {
            tracks = 1;
        }
    }

    void Update()
    {
        if (H19.Instance.serialPort != null && H19.Instance.serialPort.IsOpen)
        {
            mczCanvas.SetActive(true);
        }
        else
        {
            mczCanvas.SetActive(false);
        }
    }

    public static void InChar(byte c)
    {
        if (sendImage)
        {
            // send image handshake from Zilog - ready for next track
            if (c == 'R')
            {
                sendReady = true;
            }
            return;
        }
        if (byteIndex < diskImageBuffer.Length)
        {
            // read image input data
            int seconds = (int)(System.DateTime.Now - startTime).TotalSeconds;

            diskImageBuffer[byteIndex++] = c;

            Instance.progressSlider.value = ((float)byteIndex / diskImageBuffer.Length) * 100;
            int track = byteIndex / (Instance.bytesPerSector * Instance.sectorsPerTrack);
            Instance.progressText.text = "Track " + track.ToString() + " of 77 bytes read so far " + byteIndex.ToString() + " / " + diskImageBuffer.Length.ToString() + " Seconds " + seconds.ToString();

            if (byteIndex == diskImageBuffer.Length)
            {
                readImage = false;
                H19.Instance.disableOutput = true;
                FilePicker.Instance.onCompleteCallback += Instance.OnFileSaveSuccess;
                FilePicker.Instance.ShowPicker(true);
                //SimpleFileBrowser.FileBrowser.ShowSaveDialog(Instance.OnFileSaveSuccess, Instance.OnFileSaveCancel, SimpleFileBrowser.FileBrowser.PickMode.Files);
            }
        }
    }

    // all console input from the Zilog is routed to InChar()
    public void ReadImageButton(int device)
    {
        startTime = System.DateTime.Now;

        int bytes = bytesPerSector * sectorsPerTrack * tracks;
        diskImageBuffer = new byte[bytes];
        if (safeModeToggle.isOn)
        {
            // use PROM read functions
            H19.Instance.OutCharDirect((byte)'R');
        }
        else
        {
            // use direct read functions
            H19.Instance.OutCharDirect((byte)'G');
        }

        byteIndex = 0;
        imagerPanel.SetActive(true);
        progressSlider.value = 0;
        progressText.text = "Track 0 of 77 - Bytes read so far " + byteIndex.ToString() + " / " + diskImageBuffer.Length.ToString();
        readImage = true;
    }

    public void ReadROMButton()
    {
        startTime = System.DateTime.Now;

        int bytes = 4096;
        diskImageBuffer = new byte[bytes];
        H19.Instance.OutCharDirect((byte)'M');

        byteIndex = 0;
        imagerPanel.SetActive(true);
        progressSlider.value = 0;
        progressText.text = "Bytes so far " + byteIndex.ToString() + " / " + diskImageBuffer.Length.ToString();
        readImage = true;
    }

    // all console output is routed to OutChar()
    public void SendImageButton()
    {
        H19.Instance.disableOutput = true;
        FilePicker.Instance.onCompleteCallback += OnFilePickerLoad;
        FilePicker.Instance.ShowPicker(true);
    }

    public void FormatButton()
    {
        // sends an empty disk image in direct write mode
        sndCmd = (byte)'F';
        string path = System.IO.Path.Combine(Application.streamingAssetsPath, "MCZEMPTY.IMG");
        OnImageLoadSuccess(path);
    }

    public void CancelButton()
    {
        imagerPanel.SetActive(false);
        sendImage = false;
        readImage = false;
    }

    void OnFilePickerLoad(string filePath)
    {
        H19.Instance.disableOutput = false;
        FilePicker.Instance.onCompleteCallback -= OnFilePickerLoad;
        if (safeModeToggle.isOn)
        {
            // write using PROM functions
            sndCmd = (byte)'W';
        }
        else
        {
            // write using direct write functions
            sndCmd = (byte)'F';
        }
        OnImageLoadSuccess(filePath);
    }

    void OnImageLoadSuccess(string filePath)
    {
        if (!string.IsNullOrEmpty(filePath))
        {
            if (System.IO.File.Exists(filePath))
            {
                diskImageBuffer = System.IO.File.ReadAllBytes(filePath);

                byteIndex = 0;
                progressSlider.value = 0;
                progressText.text = string.Empty;

                ShowWarningPanel();
            }
        }
    }

    void ShowWarningPanel()
    {
        // are we sure we want to overwrite drive 0 disk?
        warningPanelRoot.SetActive(true);
    }

    public void StartSend()
    {
        byte[] data = new byte[1];
        data[0] = sndCmd; // 'W' or 'F' depending on Safe Mode toggle
        H19.Instance.serialPort.Write(data, 0, 1);

        sendImage = true;

        imagerPanel.SetActive(true);
        warningPanelRoot.SetActive(false);
        StartCoroutine(SendImageCoroutine());
    }

    public void CancelSend()
    {
        imagerPanel.SetActive(false);
        warningPanelRoot.SetActive(false);
        sendImage = false;
    }

    bool WaitForReady()
    {
        if (sendReady)
        {
            sendReady = false;
            return true;
        }
        if (!sendImage)
        {
            // send image was cancelled
            return true;
        }
        return false;
    }

    IEnumerator SendImageCoroutine()
    {
        mczCanvas.SetActive(false);

        yield return new WaitForEndOfFrame();

        progressText.text = "(.) Track 0 of 77 - Bytes written so far 0 / " + diskImageBuffer.Length.ToString() + " seconds=0";
        yield return new WaitUntil(WaitForReady);

        int track = 0;
        int seconds = 0;
        int bytes = bytesPerSector * sectorsPerTrack;
        startTime = System.DateTime.Now;
        for (int i = 0; i < diskImageBuffer.Length; i += bytes)
        {
            int beg = i;
            int end = beg + bytes;

            progressText.text = "(S) Track " + track.ToString() + " of 77 - Bytes written so far " + beg.ToString() + " / " + diskImageBuffer.Length.ToString() + " seconds=" + seconds.ToString();
            yield return new WaitForEndOfFrame();

            for (int n = beg; n < end; n++)
            {
                if ((n % bytesPerSector) == 0)
                {
                    progressSlider.value = ((float)n / diskImageBuffer.Length) * 100;
                    track = (n + 1) / bytes;
                    seconds = (int)(System.DateTime.Now - startTime).TotalSeconds;

                    progressText.text = "(S) Track " + track.ToString() + " of 77 - Bytes written so far " + n.ToString() + " / " + diskImageBuffer.Length.ToString() + " seconds=" + seconds.ToString();
                    yield return new WaitForEndOfFrame();
                }
                H19.Instance.serialPort.Write(diskImageBuffer, n, 1);
            }

            while (!WaitForReady())
            {
                progressSlider.value = ((float)end / diskImageBuffer.Length) * 100;
                track = (end + 1) / bytes;
                seconds = (int)(System.DateTime.Now - startTime).TotalSeconds;
                progressText.text = "(W) Track " + track.ToString() + " of 77 - Bytes written so far " + end.ToString() + " / " + diskImageBuffer.Length.ToString() + " seconds=" + seconds.ToString();
                yield return new WaitForEndOfFrame();
            }

            if (!sendImage)
            {
                break;
            }
        }

        mczCanvas.SetActive(true);
        imagerPanel.SetActive(false);

        sendImage = false;
    }

    // send MCZIMAGER hex code to Zilog computer
    public void SendLoader()
    {
        loaderPanel.SetActive(false);
        if (inSendCoroutine)
        {
            return;
        }
        string path = System.IO.Path.Combine(Application.streamingAssetsPath, MCZLoadDump.globalFileName);
        byte[] hexDump = MCZLoadDump.GetDumpBinary(path);
        
        List<string> hexCode = new List<string>();
        string s = "D " + loaderStartAdr; // 0x4400
        hexCode.Add(s);
        for (int i = 0; i < hexDump.Length; i++)
        {
            string h = hexDump[i].ToString("X2");
            hexCode.Add(h);
        }
        s = "Q"; // tell Zilog to exit hex code entry mode
        hexCode.Add(s);
        s = "J " + loaderStartAdr; // tell Zilog to jump to program start address
        hexCode.Add(s);

        StartCoroutine(SendTextCoroutine(hexCode.ToArray()));
    }

    public void CancelLoader()
    {
        loaderPanel.SetActive(false);
    }

    public void SendTextFile()
    {
        if (inSendCoroutine)
        {
            return;
        }
        H19.Instance.disableOutput = true;
        FilePicker.Instance.onCompleteCallback += OnFileLoadSuccess;
        FilePicker.Instance.ShowPicker(true);
    }

    void OnFileLoadSuccess(string filePath)
    {
        H19.Instance.disableOutput = false;
        FilePicker.Instance.onCompleteCallback -= OnFileLoadSuccess;
        if (!string.IsNullOrEmpty(filePath))
        {
            if (System.IO.File.Exists(filePath))
            {
                string[] fileText = System.IO.File.ReadAllLines(filePath);
                StartCoroutine(SendTextCoroutine(fileText));
            }
        }
    }

    void OnFileSaveSuccess(string filePath)
    {
        H19.Instance.disableOutput = false;
        FilePicker.Instance.onCompleteCallback -= OnFileSaveSuccess;
        if (!string.IsNullOrEmpty(filePath))
        {
            Debug.Log("Writing " + byteIndex.ToString() + " bytes to " + filePath);
            System.IO.File.WriteAllBytes(filePath, diskImageBuffer);

            Instance.imagerPanel.SetActive(false);
        }
    }

    IEnumerator SendTextCoroutine(string[] textArray, bool addCr = true)
    {
        mczCanvas.SetActive(false);
        H19.Instance.disableOutput = false;

        int delayCount = 0;

        inSendCoroutine = true;
        for (int i = 0; i < textArray.Length; i++)
        {
            string s = textArray[i];
            for (int n = 0; n < s.Length; n++)
            {
                byte c = (byte)s[n];
                H19.Instance.OutCharDirect(c);
                delayCount++;
                if (delayCount == 4)
                {
                    yield return new WaitForEndOfFrame();
                    delayCount = 0;
                }
            }
            yield return new WaitForEndOfFrame();
            yield return new WaitForEndOfFrame();
            yield return new WaitForEndOfFrame();
            yield return new WaitForEndOfFrame();
            if (addCr)
            {
                H19.Instance.OutCharDirect(0x0D);
            }
            yield return new WaitForEndOfFrame();
        }
        inSendCoroutine = false;

        mczCanvas.SetActive(false);
    }

    public void ShowSafeMode()
    {
        safeModePanel.SetActive(true);
        H19.Instance.disableOutput = true;
    }

    public void HideSafeMode()
    {
        safeModePanel.SetActive(false);
        H19.Instance.disableOutput = false;
    }

    public void ShowLoader()
    {
        loaderPanel.SetActive(true);
        H19.Instance.disableOutput = true;
    }
}

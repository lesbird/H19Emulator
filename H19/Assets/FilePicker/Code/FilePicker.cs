using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// FilePicker for Unity
/// Presents a folder view by calling "ShowPicker()" and allows to navigate between folders.
/// Files are also displayed if "ShowPicker(true)" is called which tells
/// FilePicker to require a file to be selected.
///
/// Returns with complete path of selected folder and file
///
/// public string ShowPicker(bool needFileName = false)
///
/// Callbacks
/// onCompleteCallback - called when file/folder pick has completed
/// onOpenPicker - called when picker canvas is displayed
/// onClosePicker - called when picker canvas is dismissed
/// 
/// 2021 Les Bird
/// </summary>
public class FilePicker : MonoBehaviour
{
    public GameObject panelRoot;
    public RectTransform contentRoot;
    public UnityEngine.UI.Text title;
    public UnityEngine.UI.Text pathText;
    public UnityEngine.UI.InputField fileInputField;
    public UnityEngine.UI.Button fileButtonPrefab;
    public RectTransform createFolderPanel;
    public UnityEngine.UI.InputField createFolderInputField;
    public UnityEngine.UI.Dropdown driveDropdown;

    public delegate void OnCompleteCallback(string filePath);
    public OnCompleteCallback onCompleteCallback;
    public delegate void OnOpenPickerCallback();
    public OnOpenPickerCallback onOpenPicker;
    public OnOpenPickerCallback onClosePicker;

    private string directoryPath;
    private string homeDirectoryPath;

    private bool requireFileName;

    private List<string> directoryContents = new List<string>();

    public static FilePicker Instance;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        directoryPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Desktop);
        homeDirectoryPath = directoryPath;

        if (PlayerPrefs.HasKey("filepickerpath"))
        {
            directoryPath = PlayerPrefs.GetString("filepickerpath");
        }
    }

    void Update()
    {

    }

    public void ShowPicker(bool needFileName = false)
    {
        //fileInputField.text = string.Empty;
        fileInputField.interactable = needFileName;

        if (onOpenPicker != null)
        {
            onOpenPicker();
        }

        requireFileName = needFileName;
        if (!requireFileName)
        {
            fileInputField.text = string.Empty;
        }

        directoryContents.Clear();

        string[] drives = System.IO.Directory.GetLogicalDrives();
        List<string> driveList = new List<string>(drives);
        driveDropdown.AddOptions(driveList);

        directoryContents.Add("[..]");

        if (!System.IO.Directory.Exists(directoryPath))
        {
            directoryPath = homeDirectoryPath;
        }

        pathText.text = directoryPath;

        string[] dirArray = System.IO.Directory.GetDirectories(directoryPath);
        for (int i = 0; i < dirArray.Length; i++)
        {
            string dirName = System.IO.Path.GetFileName(dirArray[i]);
            if (dirName[0] == '.')
            {
                continue;
            }
            string s = "[" + dirName + "]";
            directoryContents.Add(s);
        }

        string[] fileArray = System.IO.Directory.GetFiles(directoryPath);
        for (int i = 0; i < fileArray.Length; i++)
        {
            string fileName = System.IO.Path.GetFileName(fileArray[i]);
            if (fileName[0] == '.')
            {
                // filter out files that begin with . such as .DSStore, etc.
                continue;
            }
            string s = fileName;
            directoryContents.Add(s);
        }

        for (int i = 0; i < directoryContents.Count; i++)
        {
            UnityEngine.UI.Button fileButton = null;
            if (i < contentRoot.childCount)
            {
                fileButton = contentRoot.GetChild(i).GetComponent<UnityEngine.UI.Button>();
            }
            else
            {
                fileButton = Instantiate(fileButtonPrefab, contentRoot);
            }
            fileButton.gameObject.SetActive(true);
            fileButton.interactable = true;

            UnityEngine.UI.Text text = fileButton.GetComponentInChildren<UnityEngine.UI.Text>();
            text.text = directoryContents[i];

            if (text.text[0] != '[')
            {
                fileButton.interactable = requireFileName;
            }
        }

        for (int i = directoryContents.Count; i < contentRoot.childCount; i++)
        {
            contentRoot.GetChild(i).gameObject.SetActive(false);
        }

        panelRoot.SetActive(true);
    }

    public void HidePicker()
    {
        panelRoot.SetActive(false);

        if (onClosePicker != null)
        {
            onClosePicker();
        }
    }

    public void CancelPicker()
    {
        if (onCompleteCallback != null)
        {
            onCompleteCallback(string.Empty);
        }
        HidePicker();
    }

    public void DonePicker()
    {
        if (requireFileName && string.IsNullOrEmpty(fileInputField.text))
        {
            return;
        }

        PlayerPrefs.SetString("filepickerpath", directoryPath);

        if (onCompleteCallback != null)
        {
            onCompleteCallback(GetFileLocation());
        }
        HidePicker();
    }

    public void TopFolderButton()
    {
        directoryPath = homeDirectoryPath;
        ShowPicker(requireFileName);
    }

    public void PreviousFolderButton()
    {
        int n = directoryPath.LastIndexOf(System.IO.Path.DirectorySeparatorChar);
        if (n > 0)
        {
            directoryPath = directoryPath.Substring(0, n);
            ShowPicker(requireFileName);
        }
    }

    public void CreateFolder()
    {
        createFolderPanel.gameObject.SetActive(true);
    }

    public void CancelFolderButton()
    {
        createFolderPanel.gameObject.SetActive(false);
    }

    public void CreateFolderButton()
    {
        string pathStr = createFolderInputField.text;
        if (!string.IsNullOrEmpty(pathStr))
        {
            string folderPath = System.IO.Path.Combine(pathText.text, pathStr);
            System.IO.DirectoryInfo dir = System.IO.Directory.CreateDirectory(folderPath);
            if (dir.Exists)
            {
                directoryPath = folderPath;
                ShowPicker(requireFileName);
            }
        }
        createFolderPanel.gameObject.SetActive(false);
    }

    public void SelectFile()
    {
        UnityEngine.UI.Button button = UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject.GetComponent<UnityEngine.UI.Button>();
        UnityEngine.UI.Text text = button.GetComponentInChildren<UnityEngine.UI.Text>();
        if (text.text[0] == '[')
        {
            if (text.text.Equals("[..]"))
            {
                PreviousFolderButton();
                return;
            }
            char[] trimChars = { '[', ']' };
            string folder = text.text.Trim(trimChars);
            directoryPath = System.IO.Path.Combine(directoryPath, folder);
            ShowPicker(requireFileName);
        }
        else
        {
            if (requireFileName)
            {
                fileInputField.text = text.text;
            }
        }
    }

    public void ChangeDrive(int driveIdx)
    {
        string driveStr = driveDropdown.captionText.text;
        directoryPath = driveStr;
        ShowPicker(requireFileName);
    }

    public string GetFolderName()
    {
        return directoryPath;
    }

    public string GetFileName()
    {
        return fileInputField.text;
    }

    public string GetFileLocation()
    {
        string filePath = System.IO.Path.Combine(directoryPath, fileInputField.text);
        return filePath;
    }
}

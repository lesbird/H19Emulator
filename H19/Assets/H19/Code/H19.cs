using System.Collections;
using System.Collections.Generic;
using System.IO.Ports;
using UnityEngine;

public class H19 : MonoBehaviour
{
	public bool useSerialPort;
	public UnityEngine.UI.Text h19DebugText;
	//public MeshRenderer[,] h19Screen = new MeshRenderer[80, 25];
	//public MeshRenderer h19Cursor;
	public MeshRenderer h19FontChar;
	private H19Font h19Font;

	public Canvas configScreen;
	public UnityEngine.UI.Dropdown baudDropDown;
	public UnityEngine.UI.Dropdown comDropDown;

	public UnityEngine.UI.Toggle offlineCheckbox;
	public UnityEngine.UI.Toggle colorGreen;
	public UnityEngine.UI.Toggle colorAmber;
	public UnityEngine.UI.Toggle colorWhite;

	public Color screenColorGreen;
	public Color screenColorAmber;
	public Color screenColorWhite;

	private Color screenColor = Color.green;
	[SerializeField]
	private int screenColorIndex;

	public UnityEngine.UI.Slider autoRepeatDelay;
	public UnityEngine.UI.Text autoRepeatDelayValue;
	public UnityEngine.UI.Slider autoRepeatRate;
	public UnityEngine.UI.Text autoRepeatRateValue;
	private float autoRepeatTimer;
	private float autoRepeatRateTimer;
	private byte autoRepeatChar;

	public UnityEngine.UI.Text versionText;

	private Texture2D h19ScreenTexture;
	public Renderer h19ScreenRenderer;

	public UnityEngine.UI.Slider s401_1;
	public UnityEngine.UI.Slider s401_2;
	public UnityEngine.UI.Slider s401_3;
	public UnityEngine.UI.Slider s401_4;
	public UnityEngine.UI.Slider s401_5;
	public UnityEngine.UI.Slider s401_6;
	public UnityEngine.UI.Slider s401_7;
	public UnityEngine.UI.Slider s401_8;

	public UnityEngine.UI.Text s401_1_result;
	public UnityEngine.UI.Text s401_2_result;
	public UnityEngine.UI.Text s401_3_result;
	public UnityEngine.UI.Text s401_4_result;
	public UnityEngine.UI.Text s401_5_result;
	public UnityEngine.UI.Text s401_6_result;
	public UnityEngine.UI.Text s401_7_result;
	public UnityEngine.UI.Text s401_8_result;

	public UnityEngine.UI.Slider s402_1;
	public UnityEngine.UI.Slider s402_2;
	public UnityEngine.UI.Slider s402_3;
	public UnityEngine.UI.Slider s402_4;
	public UnityEngine.UI.Slider s402_5;
	public UnityEngine.UI.Slider s402_6;
	public UnityEngine.UI.Slider s402_7;
	public UnityEngine.UI.Slider s402_8;

	public UnityEngine.UI.Text s402_1_result;
	public UnityEngine.UI.Text s402_2_result;
	public UnityEngine.UI.Text s402_3_result;
	public UnityEngine.UI.Text s402_4_result;
	public UnityEngine.UI.Text s402_5_result;
	public UnityEngine.UI.Text s402_6_result;
	public UnityEngine.UI.Text s402_7_result;
	public UnityEngine.UI.Text s402_8_result;

	public UnityEngine.UI.Toggle capsLockToggle;
	public UnityEngine.UI.Toggle logToggle;

	private Vector3 savePos;
	private int column;
	private int line;
	private byte[] vidmem = new byte[80 * 25];
	private bool dirty;

	// 0-3=baud rate
	// 4=no parity/parity
	// 5=odd parity/even parity
	// 6=normal parity/?
	// 7=half duplex/full duplex
	public byte[] S401;
	// 0=underscore (0)/block cursor (1)
	// 1=key click (0)/no key click (1)
	// 2=discard past end of line/wrap around
	// 3=no auto-lf on cr/auto-lf on cr
	// 4=no auto-cr on lf/auto-cr on lf
	// 5=heath mode/ansi mode
	// 6=keypad normal/keypad shifted
	// 7=60hz/50hz
	public byte[] S402;

	private bool dca; // direct cursor address
	private int dcaIndex;
	private bool escSeq;
	private int escSeqIndex;
	private bool specialMode;
	private bool resetMode;
	private bool baudSeq;
	public bool offline;
	public bool halfDuplex;

	private byte[] ansiSeq = new byte[8];

	private int lineCursor = 27;
	private int blockCursor = 144;
	private int cursorType;
	private float blinkTimer;

	// states
	public bool enableANSIMode;
	public bool enableBlockCursor;
	public bool enableCursor;
	public bool enableInsertChar;
	public bool enableKeyboard;
	public bool enableKeyClick;
	public bool enableLine25;
	public bool enableLFonCR;
	public bool enableCRonLF;
	public bool enableWrapAround;
	public bool graphMode;
	public bool reverse;
	public bool shift;
	public bool capsLock;
	public bool control;
	public bool keypadShifted;
	public bool altKeypadMode;

	// I/O ports
	[HideInInspector]
	public SerialPort serialPort;
	private byte[] readBuffer = new byte[1024];
	private byte[] writeBuffer = new byte[8192];
	[HideInInspector]
	public int writeIndex;

	[HideInInspector]
	public bool disableOutput;

	private bool cursorVis;

	private bool[] bluetoothKeyCode = new bool[512];
	private bool[] keyCodeDown = new bool[512];
	private bool[] lastKeyCodeDown = new bool[512];

	public delegate void OnConnect();
	public OnConnect onConnect;

	public static H19 Instance;

    void Awake()
    {
		Instance = this;
    }

    void Start()
	{
		versionText.text = "V" + Application.version;

		cursorType = lineCursor;

		InitCOMPortItems();

		InitScreen();
		SetDefaults();
		LoadConfig();
		ShowConfig();
#if UNITY_IOS
		ExternalInputController.instance.OnExternalInputDidReceiveKeyDown += Instance_OnExternalInputDidReceiveKeyDown;
        ExternalInputController.instance.OnExternalInputDidReceiveKeyUp += Instance_OnExternalInputDidReceiveKeyUp;
#endif
	}

#if UNITY_IOS
	private void Instance_OnExternalInputDidReceiveKeyUp(KeyCode keyCode)
    {
		bluetoothKeyCode[(int)keyCode] = false;
		//Debug.Log("OnExternalInputDidReceiveKeyUp() keyCode=" + keyCode.ToString());
    }

    private void Instance_OnExternalInputDidReceiveKeyDown(KeyCode keyCode)
    {
		bluetoothKeyCode[(int)keyCode] = true;
		//Debug.Log("OnExternalInputDidReceiveKeyDown() keyCode=" + keyCode.ToString());
	}
#endif

	void OnDestroy()
	{
		//SaveLog();
    }

    void InitScreen()
	{
		h19ScreenTexture = new Texture2D(640, 250, TextureFormat.ARGB32, false, false);
		h19ScreenRenderer.material.mainTexture = h19ScreenTexture;

		int n = 0;
		for (int j = 0; j < 25; j++)
		{
			for (int i = 0; i < 80; i++)
			{
				//h19Screen[i, j] = Instantiate<MeshRenderer>(h19FontChar);
				//h19Screen[i, j].transform.parent = transform;
				//h19Screen[i, j].transform.localScale = new Vector3(8, -20, 1);
				//h19Screen[i, j].transform.position = new Vector3(-320 + (i * 8), 240 - (j * 20), 0);

				vidmem[n++] = 0x20;
			}
		}
		h19Font = GameObject.FindObjectOfType<H19Font>();

		//h19Cursor = Instantiate<MeshRenderer>(h19FontChar);
		//h19Cursor.transform.parent = transform;
		//h19Cursor.transform.localScale = new Vector3(8, -20, 1);
		//h19Cursor.transform.position = new Vector3(-320 + (column * 8), 240 - (line * 20), -1);
		//h19Cursor.material.mainTexture = h19Font.fontCharArray[27];
	}

	void SetDefaults()
	{
		enableLFonCR = (S402[3] == 0) ? false : true;
		enableCRonLF = (S402[4] == 0) ? false : true;
		enableANSIMode = (S402[5] == 0) ? false : true;
		enableBlockCursor = (S402[0] == 0) ? false : true;
		enableCursor = true;
		enableInsertChar = false;
		enableKeyboard = true;
		enableKeyClick = (S402[1] == 0) ? true : false;
		enableWrapAround = (S402[2] == 0) ? false : true;

		escSeq = false;
		graphMode = false;
		reverse = false;
		specialMode = false;
		resetMode = false;
		capsLock = false;
		shift = false;
		control = false;
		keypadShifted = (S402[6] == 0) ? false : true;
		altKeypadMode = false;

		SetCursorHome();
		Disable25();

		EraseScreen();
	}

	void Update()
	{
		UpdateConfigSwitches();
		HandleSerialPort();
		HandleKeyboardInput();
		UpdateVideo();
	}

	void UpdateConfigSwitches()
	{
		if (configScreen.gameObject.activeInHierarchy)
		{
			S401[0] = (byte)s401_1.value;
			S401[1] = (byte)s401_2.value;
			S401[2] = (byte)s401_3.value;
			S401[3] = (byte)s401_4.value;
			S401[4] = (byte)s401_5.value;
			S401[5] = (byte)s401_6.value;
			S401[6] = (byte)s401_7.value;
			S401[7] = (byte)s401_8.value;

			S402[0] = (byte)s402_1.value;
			S402[1] = (byte)s402_2.value;
			S402[2] = (byte)s402_3.value;
			S402[3] = (byte)s402_4.value;
			S402[4] = (byte)s402_5.value;
			S402[5] = (byte)s402_6.value;
			S402[6] = (byte)s402_7.value;
			S402[7] = (byte)s402_8.value;

			ShowConfigSettings();
		}
		/*
		else
		{
			s401_1.value = S401[0];
			s401_2.value = S401[1];
			s401_3.value = S401[2];
			s401_4.value = S401[3];
			s401_5.value = S401[4];
			s401_6.value = S401[5];
			s401_7.value = S401[6];
			s401_8.value = S401[7];

			s402_1.value = S401[0];
			s402_2.value = S401[1];
			s402_3.value = S401[2];
			s402_4.value = S401[3];
			s402_5.value = S401[4];
			s402_6.value = S401[5];
			s402_7.value = S401[6];
			s402_8.value = S401[7];
		}
		*/
	}

	void UpdateVideo()
	{
		// update Unity screen to represent H19 screen
		RenderScreen();
	}

	bool InputGetKeyDown(KeyCode keyCode)
	{
		if (lastKeyCodeDown[(int)keyCode])
		{
			return false;
		}
		return InputGetKey(keyCode);
	}

	bool InputGetKeyUp(KeyCode keyCode)
	{
		bool state = keyCodeDown[(int)keyCode];
		if (!state && lastKeyCodeDown[(int)keyCode])
		{
			return true;
		}
		return false;
	}

	bool InputGetKey(KeyCode keyCode)
	{
		return keyCodeDown[(int)keyCode];
	}

	void InputUpdateCurrentState()
	{
		for (int i = 0; i < 512; i++)
		{
			KeyCode keyCode = (KeyCode)i;
			if (Input.GetKey(keyCode))
			{
				keyCodeDown[i] = true;
			}
			else
			{
				keyCodeDown[i] = bluetoothKeyCode[i];
			}
		}
	}

	void InputUpdateLastState()
	{
		for (int i = 0; i < 512; i++)
		{
			lastKeyCodeDown[i] = keyCodeDown[i];
		}
	}

	void HandleKeyboardInput()
	{
		HandleAutoRepeat();

		InputUpdateCurrentState();

		if (InputGetKeyDown(KeyCode.F12))
		{
			ShowConfig();
			return;
		}
		if (InputGetKey(KeyCode.LeftShift) || InputGetKey(KeyCode.RightShift))
		{
			if (InputGetKeyDown(KeyCode.Escape))
			{
				Application.Quit();
				return;
			}
		}
		if (configScreen.gameObject.activeInHierarchy)
		{
			if (InputGetKeyDown(KeyCode.Return) || InputGetKeyDown(KeyCode.KeypadEnter))
			{
				Connect();
			}
		}
		else if (enableKeyboard)
		{
			byte c = KeyboardKey();
			if (c != 0)
			{
				autoRepeatChar = c;
				HandleKeyboardChar(c);
			}
		}

		InputUpdateLastState();
	}

	void HandleKeyboardChar(byte c)
	{
		if (shift)
		{
			c = HandleShiftedKey(c);
		}
		if (control)
		{
			c = HandleControlKey(c);
		}
		if (capsLock)
		{
			c = (byte)char.ToUpper((char)c);
		}
		if (halfDuplex)
		{
			InChar(c);
		}
		OutChar(c);
	}

	void HandleAutoRepeat()
	{
		if (autoRepeatDelay.value > 0)
		{
			KeyCode n = (KeyCode)autoRepeatChar;
			if (InputGetKey(n))
			{
				float delay = autoRepeatDelay.value / 30;
				autoRepeatTimer += Time.deltaTime;
				if (autoRepeatTimer >= delay)
				{
					float rate = Mathf.Max(1, autoRepeatRate.value);
					float rateDelay = 1.0f / rate;
					autoRepeatRateTimer += Time.deltaTime;
					if (autoRepeatRateTimer >= rateDelay)
					{
						HandleKeyboardChar(autoRepeatChar);
						autoRepeatRateTimer = 0;
					}
				}
			}
			else
			{
				autoRepeatRateTimer = 0;
				autoRepeatTimer = 0;
			}
		}
	}

	byte HandleShiftedKey(byte c)
	{
		//Debug.Log("HandleShiftedKey() c=" + c.ToString());

		if (shift)
		{
			if (c == ';')
			{
				c = (byte)':';
			}
			else if (c == '\'')
			{
				c = (byte)'\"';
			}
			else if (c == ',')
			{
				c = (byte)'<';
			}
			else if (c == '.')
			{
				c = (byte)'>';
			}
			else if (c == '[')
			{
				c = (byte)'{';
			}
			else if (c == ']')
			{
				c = (byte)'}';
			}
			else if (c == '\\')
			{
				c = (byte)'|';
			}
			else if (c == '`')
			{
				c = (byte)'~';
			}
			else if (c == '1')
			{
				c = (byte)'!';
			}
			else if (c == '2')
			{
				c = (byte)'@';
			}
			else if (c == '3')
			{
				c = (byte)'#';
			}
			else if (c == '4')
			{
				c = (byte)'$';
			}
			else if (c == '5')
			{
				c = (byte)'%';
			}
			else if (c == '6')
			{
				c = (byte)'^';
			}
			else if (c == '7')
			{
				c = (byte)'&';
			}
			else if (c == '8')
			{
				c = (byte)'*';
			}
			else if (c == '9')
			{
				c = (byte)'(';
			}
			else if (c == '0')
			{
				c = (byte)')';
			}
			else if (c == '-')
			{
				c = (byte)'_';
			}
			else if (c == '=')
			{
				c = (byte)'+';
			}
			else if (c == '/')
			{
				c = (byte)'?';
			}
		}
		if (c >= 'a' && c <= 'z')
		{
			c &= 0x5F;
		}

		return c;
	}

	byte HandleControlKey(byte c)
	{
		c &= 0x1F;
		return c;
	}

	byte KeyboardKey()
	{
		if (InputGetKey(KeyCode.LeftShift) || InputGetKey(KeyCode.RightShift))
		{
			shift = true;
		}
		else
		{
			shift = false;
		}
		if (InputGetKey(KeyCode.LeftControl) || InputGetKey(KeyCode.RightControl))
		{
			control = true;
		}
		else
		{
			control = false;
		}
		/*
		if (InputGetKeyDown(KeyCode.CapsLock))
		{
			capsLock = !capsLock;
			Debug.Log("capsLock=" + capsLock.ToString());
		}
		*/

		capsLock = capsLockToggle.isOn;

		//else
		{
			if (altKeypadMode)
			{
				byte c = (enableANSIMode) ? (byte)'O' : (byte)'?';
				if (InputGetKeyDown(KeyCode.Keypad1))
				{
					OutChar(0x1B); // ESC
					OutChar(c);
					OutChar((byte)'q');
					return 0;
				}
				if (InputGetKeyDown(KeyCode.Keypad2) || InputGetKeyDown(KeyCode.DownArrow))
				{
					OutChar(0x1B); // ESC
					OutChar(c);
					OutChar((byte)'r');
					return 0;
				}
				if (InputGetKeyDown(KeyCode.Keypad3))
				{
					OutChar(0x1B); // ESC
					OutChar(c);
					OutChar((byte)'s');
					return 0;
				}
				if (InputGetKeyDown(KeyCode.Keypad4) || InputGetKeyDown(KeyCode.LeftArrow))
				{
					OutChar(0x1B); // ESC
					OutChar(c);
					OutChar((byte)'t');
					return 0;
				}
				if (InputGetKeyDown(KeyCode.Keypad5))
				{
					OutChar(0x1B); // ESC
					OutChar(c);
					OutChar((byte)'u');
					return 0;
				}
				if (InputGetKeyDown(KeyCode.Keypad6) || InputGetKeyDown(KeyCode.RightArrow))
				{
					OutChar(0x1B); // ESC
					OutChar(c);
					OutChar((byte)'v');
					return 0;
				}
				if (InputGetKeyDown(KeyCode.Keypad7))
				{
					OutChar(0x1B); // ESC
					OutChar(c);
					OutChar((byte)'w');
					return 0;
				}
				if (InputGetKeyDown(KeyCode.Keypad8) || InputGetKeyDown(KeyCode.UpArrow))
				{
					OutChar(0x1B); // ESC
					OutChar(c);
					OutChar((byte)'x');
					return 0;
				}
				if (InputGetKeyDown(KeyCode.Keypad9))
				{
					OutChar(0x1B); // ESC
					OutChar(c);
					OutChar((byte)'y');
					return 0;
				}
				if (InputGetKeyDown(KeyCode.KeypadEnter))
				{
					OutChar(0x1B);
					OutChar(c);
					OutChar((byte)'M');
					return 0;
				}
				if (InputGetKeyDown(KeyCode.KeypadPeriod))
				{
					OutChar(0x1B);
					OutChar(c);
					OutChar((byte)'n');
					return 0;
				}
				if (InputGetKeyDown(KeyCode.Keypad0))
				{
					OutChar(0x1B);
					OutChar(c);
					OutChar((byte)'p');
					return 0;
				}
			}
			else if (keypadShifted)
			{
				if (InputGetKeyDown(KeyCode.Keypad1))
				{
					OutChar(0x1B); // ESC
					OutChar((byte)'L');
					return 0;
				}
				if (InputGetKeyDown(KeyCode.Keypad2) || InputGetKeyDown(KeyCode.DownArrow))
				{
					OutChar(0x1B); // ESC
					OutChar((byte)'B');
					return 0;
				}
				if (InputGetKeyDown(KeyCode.Keypad3))
				{
					OutChar(0x1B); // ESC
					OutChar((byte)'M');
					return 0;
				}
				if (InputGetKeyDown(KeyCode.Keypad4) || InputGetKeyDown(KeyCode.LeftArrow))
				{
					OutChar(0x1B); // ESC
					OutChar((byte)'D');
					return 0;
				}
				if (InputGetKeyDown(KeyCode.Keypad5))
				{
					OutChar(0x1B); // ESC
					OutChar((byte)'H');
					return 0;
				}
				if (InputGetKeyDown(KeyCode.Keypad6) || InputGetKeyDown(KeyCode.RightArrow))
				{
					OutChar(0x1B); // ESC
					OutChar((byte)'C');
					return 0;
				}
				if (InputGetKeyDown(KeyCode.Keypad7))
				{
					OutChar(0x1B); // ESC
					OutChar((byte)'@');
					return 0;
				}
				if (InputGetKeyDown(KeyCode.Keypad8) || InputGetKeyDown(KeyCode.UpArrow))
				{
					OutChar(0x1B); // ESC
					OutChar((byte)'A');
					return 0;
				}
				if (InputGetKeyDown(KeyCode.Keypad9))
				{
					OutChar(0x1B); // ESC
					OutChar((byte)'N');
					return 0;
				}
				if (InputGetKeyDown(KeyCode.KeypadEnter))
				{
					return 0x0D;
				}
				if (InputGetKeyDown(KeyCode.KeypadPeriod))
				{
					return (byte)'.';
				}
				if (InputGetKeyDown(KeyCode.Keypad0))
				{
					return (byte)'0';
				}
			}
			
			{
				if (InputGetKeyDown(KeyCode.Keypad1))
				{
					return (byte)'1';
				}
				if (InputGetKeyDown(KeyCode.Keypad2))
				{
					return (byte)'2';
				}
				if (InputGetKeyDown(KeyCode.Keypad3))
				{
					return (byte)'3';
				}
				if (InputGetKeyDown(KeyCode.Keypad4))
				{
					return (byte)'4';
				}
				if (InputGetKeyDown(KeyCode.Keypad5))
				{
					return (byte)'5';
				}
				if (InputGetKeyDown(KeyCode.Keypad6))
				{
					return (byte)'6';
				}
				if (InputGetKeyDown(KeyCode.Keypad7))
				{
					return (byte)'7';
				}
				if (InputGetKeyDown(KeyCode.Keypad8))
				{
					return (byte)'8';
				}
				if (InputGetKeyDown(KeyCode.Keypad9))
				{
					return (byte)'9';
				}
				if (InputGetKeyDown(KeyCode.DownArrow))
				{
					OutChar(0x1B); // ESC
					OutChar((byte)'B');
					return 0;
				}
				if (InputGetKeyDown(KeyCode.LeftArrow))
				{
					OutChar(0x1B); // ESC
					OutChar((byte)'D');
					return 0;
				}
				if (InputGetKeyDown(KeyCode.RightArrow))
				{
					OutChar(0x1B); // ESC
					OutChar((byte)'C');
					return 0;
				}
				if (InputGetKeyDown(KeyCode.UpArrow))
				{
					OutChar(0x1B); // ESC
					OutChar((byte)'A');
					return 0;
				}
				if (InputGetKeyDown(KeyCode.KeypadEnter))
				{
					return 0x0D;
				}
				if (enableANSIMode)
				{
					if (InputGetKeyDown(KeyCode.F1))
					{
						OutChar(0x1B);
						OutChar((byte)'O');
						OutChar((byte)'S');
						return 0;
					}
					if (InputGetKeyDown(KeyCode.F2))
					{
						OutChar(0x1B);
						OutChar((byte)'O');
						OutChar((byte)'T');
						return 0;
					}
					if (InputGetKeyDown(KeyCode.F3))
					{
						OutChar(0x1B);
						OutChar((byte)'O');
						OutChar((byte)'U');
						return 0;
					}
					if (InputGetKeyDown(KeyCode.F4))
					{
						OutChar(0x1B);
						OutChar((byte)'O');
						OutChar((byte)'V');
						return 0;
					}
					if (InputGetKeyDown(KeyCode.F5))
					{
						OutChar(0x1B);
						OutChar((byte)'O');
						OutChar((byte)'W');
						return 0;
					}
					if (InputGetKeyDown(KeyCode.F6)) // ERASE
					{
						if (shift)
						{
							OutChar(0x1B);
							OutChar((byte)'E');
						}
						else
						{
							OutChar(0x1B);
							OutChar((byte)'J');
						}
						return 0;
					}
					if (InputGetKeyDown(KeyCode.F7)) // blue
					{
						OutChar(0x1B);
						OutChar((byte)'O');
						OutChar((byte)'P');
						return 0;
					}
					if (InputGetKeyDown(KeyCode.F8)) // red
					{
						OutChar(0x1B);
						OutChar((byte)'O');
						OutChar((byte)'Q');
						return 0;
					}
					if (InputGetKeyDown(KeyCode.F9)) // gray
					{
						OutChar(0x1B);
						OutChar((byte)'O');
						OutChar((byte)'R');
						return 0;
					}
				}
				else
				{
					if (InputGetKeyDown(KeyCode.F1))
					{
						OutChar(0x1B);
						OutChar((byte)'S');
						return 0;
					}
					if (InputGetKeyDown(KeyCode.F2))
					{
						OutChar(0x1B);
						OutChar((byte)'T');
						return 0;
					}
					if (InputGetKeyDown(KeyCode.F3))
					{
						OutChar(0x1B);
						OutChar((byte)'U');
						return 0;
					}
					if (InputGetKeyDown(KeyCode.F4))
					{
						OutChar(0x1B);
						OutChar((byte)'V');
						return 0;
					}
					if (InputGetKeyDown(KeyCode.F5))
					{
						OutChar(0x1B);
						OutChar((byte)'W');
						return 0;
					}
					if (InputGetKeyDown(KeyCode.F6)) // ERASE
					{
						if (shift)
						{
							OutChar(0x1B);
							OutChar((byte)'E');
						}
						else
						{
							OutChar(0x1B);
							OutChar((byte)'J');
						}
						return 0;
					}
					if (InputGetKeyDown(KeyCode.F7)) // blue
					{
						OutChar(0x1B);
						OutChar((byte)'P');
						return 0;
					}
					if (InputGetKeyDown(KeyCode.F8)) // red
					{
						OutChar(0x1B);
						OutChar((byte)'Q');
						return 0;
					}
					if (InputGetKeyDown(KeyCode.F9)) // gray
					{
						OutChar(0x1B);
						OutChar((byte)'R');
						return 0;
					}
				}
				if (InputGetKeyDown(KeyCode.F10))
				{
					if (shift)
					{
						ResetToPowerUpConfig();
						EraseScreen();
					}
					return 0;
				}
			}
			for (int i = 0; i < 128; i++)
			{
				if (InputGetKeyDown((KeyCode)i))
				{
					return (byte)i;
				}
			}
		}
		return 0;
	}

	void InChar(byte c)
	{
		if (logToggle.isOn)
		{
			if (c == 0)
			{
				return;
			}
			LogByte(c);
		}
		if (MCZImager.readImage)
		{
			MCZImager.InChar(c);
			return;
		}
		if (MCZImager.sendImage)
		{
			MCZImager.InChar(c);
			return;
		}
		dirty = true;
		if (enableANSIMode)
		{
			c &= 0x7F;
		}
		if (escSeq)
		{
			// escape command
			escSeqIndex = 0;
			HandleESCSeq(c);
		}
		else if (c == 0x1B)
		{
			escSeq = true;
		}
		else if (c >= 0x20 && c < 0x7F)
		{
			int m = (line * 80) + column;
			if (graphMode)
			{
				if (c == '^')
				{
					c = 127;
				}
				else if (c == '_')
				{
					c = 31;
				}
				else if (c >= 96 && c <= 126)
				{
					c -= 96;
				}
			}
			if (reverse)
			{
				c += 128;
			}

			if (enableInsertChar)
			{
				int e = ((line * 80) + 80) - 1;
				for (int i = e; i > m; i--)
				{
					vidmem[i] = vidmem[i - 1];
				}
			}

			vidmem[m] = c;

			column++;
			if (column == 80)
			{
				if (enableWrapAround)
				{
					column = 0;
					NextLine();
				}
				else
				{
					column = 79;
				}
			}
		}
		else if (c == 0x08)
		{
			SetCursorBackward();
		}
		else if (c == 0x09)
		{
			int startColumn = column;
			do
			{
				column++;
			} while ((column % 8) != 0);

			if (column >= 80)
			{
				column = Mathf.Min(startColumn + 1, 79);
			}
		}
		else if (c == 0x0D)
		{
			column = 0;
			if (enableLFonCR)
			{
				NextLine();
			}
		}
		else if (c == 0x0A)
		{
			if (enableCRonLF)
			{
				column = 0;
			}
			NextLine();
		}
	}

	void ShowESCSeqChar(byte c)
	{
		ShowESCSeqCharVid((byte)'{');
		string s = c.ToString("X3");
		for (int i = 0; i < s.Length; i++)
		{
			byte n = (byte)s[i];
			ShowESCSeqCharVid(n);
		}
		ShowESCSeqCharVid((byte)'}');
	}

	void ShowESCSeqCharVid(byte c)
	{
		int m = (line * 80) + column;
		vidmem[m] = c;

		column++;
		if (column >= 80)
		{
			column = 0;
			NextLine();
		}
	}

	void PrevLine()
	{
		if (line == 24)
		{
			return;
		}
		if (line == 0)
		{
			ScrollDown();
		}
		else
		{
			line--;
		}
	}

	void NextLine()
	{
		if (line == 24)
		{
			return;
		}
		if (line == 23)
		{
			ScrollUp();
		}
		else
		{
			line++;
		}
	}

	void OutChar(byte c)
	{
		//Debug.Log("OutChar() c=" + c.ToString());
		if (MCZImager.readImage)
		{
			return;
		}

		dirty = true;
		if (writeIndex < writeBuffer.Length)
		{
			writeBuffer[writeIndex++] = c;
		}
	}

	void HandleESCSeq(byte c)
	{
		if (enableANSIMode)
		{
			HandleANSIESCSeq(c);
			return;
		}
		if (dca)
		{
			DirectCursorAddress(c);
			return;
		}

		//Debug.Log("HandleESCSeq() c=" + c.ToString());

		if (specialMode)
		{
			SetSpecialMode(c);
			specialMode = false;
			resetMode = false;
			escSeq = false;
			return;
		}

		if (baudSeq)
		{
			// Bn
			baudSeq = false;
			escSeq = false;
			return;
		}

		if (c == 'H')
		{
			SetCursorHome();
		}
		else if (c == 'C')
		{
			SetCursorForward();
		}
		else if (c == 'D')
		{
			SetCursorBackward();
		}
		else if (c == 'B')
		{
			SetCursorDown();
		}
		else if (c == 'A')
		{
			SetCursorUp();
		}
		else if (c == 'I')
		{
			PrevLine();
		}
		else if (c == 'n')
		{
			// report cursor position
			OutChar(0x1B); // ESC
			OutChar((byte)'Y'); // Y
			OutChar((byte)(line + 32));
			OutChar((byte)(column + 32));
		}
		else if (c == 'j')
		{
			SaveCursorPosition();
		}
		else if (c == 'k')
		{
			RestoreCursorPosition();
		}
		else if (c == 'Y')
		{
			dca = true;
			dcaIndex = 0;
			return;
		}
		else if (c == 'E')
		{
			EraseScreen();
		}
		else if (c == 'b')
		{
			EraseToBeginningOfScreen();
		}
		else if (c == 'J')
		{
			EraseToEndOfScreen();
		}
		else if (c == 'l')
		{
			EraseLine();
		}
		else if (c == 'o')
		{
			EraseToBeginningOfLine();
		}
		else if (c == 'K')
		{
			EraseToEndOfLine();
		}
		else if (c == 'L')
		{
			InsertLine();
		}
		else if (c == 'M')
		{
			DeleteLine();
		}
		else if (c == 'N')
		{
			DeleteChar();
		}
		else if (c == '@')
		{
			EnterInsertCharMode();
		}
		else if (c == 'O')
		{
			ExitInsertCharMode();
		}
		else if (c == 'z')
		{
			ResetToPowerUpConfig();
		}
		else if (c == 'G')
		{
			graphMode = false;
		}
		else if (c == 'F')
		{
			graphMode = true;
		}
		else if (c == 'p')
		{
			reverse = true;
		}
		else if (c == 'q')
		{
			reverse = false;
		}
		else if (c == 't')
		{
			// keypad shifted mode
			keypadShifted = true;
		}
		else if (c == 'u')
		{
			// keypad unshifted mode
			keypadShifted = false;
		}
		else if (c == '=')
		{
			// alternate keypad mode
			altKeypadMode = true;
		}
		else if (c == '>')
		{
			// exit alternate keypad mode
			altKeypadMode = false;
		}
		else if (c == 'x')
		{
			resetMode = false;
			specialMode = true;
			return;
		}
		else if (c == 'y')
		{
			resetMode = true;
			specialMode = true;
			return;
		}
		else if (c == '<')
		{
			enableANSIMode = true;
		}
		else if (c == '}')
		{
			// disable keyboard
			enableKeyboard = false;
		}
		else if (c == '{')
		{
			// enable keyboard
			enableKeyboard = true;
		}
		else if (c == 'v')
		{
			// enable wrap around at eol
			enableWrapAround = true;
		}
		else if (c == 'w')
		{
			// disable wrap around at eol
			enableWrapAround = false;
		}
		else if (c == 'Z')
		{
			// VT52
			OutChar(0x1B);
			OutChar((byte)'/');
			OutChar((byte)'K');
		}
		else if (c == ']')
		{
			// xmit 25th line
			XmitLine25();
		}
		else if (c == '#')
		{
			// xmit page
			XmitPage();
		}
		else if (c == 'r')
		{
			baudSeq = true;
			return;
		}

		//char ch = (char)c;
		//Debug.Log("HandleESCSeq() c=" + ch.ToString() + " line=" + line.ToString() + " column=" + column.ToString());

		escSeq = false;
	}

	void HandleANSIESCSeq(byte c)
	{
		switch (escSeqIndex)
		{
			case 0:
				if (c == '[')
				{
					escSeqIndex++;
					return;
				}
				else if (c == 'M')
				{
					// reverse index
					PrevLine();
				}
				break;
			default:
				if ((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z'))
				{
					PerformANSISeq(c);
				}
				else
				{
					ansiSeq[escSeqIndex] = c;
					escSeqIndex++;
					return;
				}
				break;
		}
		escSeq = false;
	}

	int ansiValueIndex;

	int GetANSIValue(int n)
	{
		int v = 0;
		for (int i = n; i < escSeqIndex; i++)
		{
			if (ansiSeq[i] == ';')
			{
				ansiValueIndex = i + 1;
				return v;
			}
			if (v != 0)
			{
				v *= 10;
			}
			v += (ansiSeq[i] - '0');
		}
		return v;
	}

	void PerformANSISeq(byte c)
	{
		if (c == 'H' || c == 'f')
		{
			if (escSeqIndex == 1)
			{
				SetCursorHome();
			}
			else
			{
				// line ; column
				int y = GetANSIValue(1);
				int x = GetANSIValue(ansiValueIndex);
				//int y = ansiSeq[1];
				//if (y == 0)
				//{
				//	y = 1;
				//}
				//int x = ansiSeq[3];
				//if (x == 0)
				//{
				//	x = 1;
				//}
				SetCursorAddress(x - 1, y - 1);
			}
		}
		else if (c == 'C')
		{
			int n = GetANSIValue(1);
			if (n == 0)
			{
				n = 1;
			}
			for (int i = 0; i < n; i++)
			{
				SetCursorForward();
			}
		}
		else if (c == 'D')
		{
			int n = GetANSIValue(1);
			if (n == 0)
			{
				n = 1;
			}
			for (int i = 0; i < n; i++)
			{
				SetCursorBackward();
			}
		}
		else if (c == 'B')
		{
			int n = GetANSIValue(1);
			if (n == 0)
			{
				n = 1;
			}
			for (int i = 0; i < n; i++)
			{
				SetCursorDown();
			}
		}
		else if (c == 'A')
		{
			int n = GetANSIValue(1);
			if (n == 0)
			{
				n = 1;
			}
			for (int i = 0; i < n; i++)
			{
				SetCursorUp();
			}
		}
		else if (c == 'n')
		{
			OutChar(0x1B);
			OutChar((byte)'[');
			string s1 = line.ToString();
			for (int i = 0; i < s1.Length; i++)
			{
				OutChar((byte)s1[i]);
			}
			//OutChar((byte)line);
			OutChar((byte)';');
			string s2 = column.ToString();
			for (int i = 0; i < s2.Length; i++)
			{
				OutChar((byte)s2[i]);
			}
			//OutChar((byte)column);
			OutChar((byte)'R');
		}
		else if (c == 's')
		{
			SaveCursorPosition();
		}
		else if (c == 'u')
		{
			RestoreCursorPosition();
		}
		else if (c == 'J')
		{
			int n = GetANSIValue(1);
			switch (n)
			{
				case 0:
					EraseToEndOfScreen();
					break;
				case 1:
					EraseToBeginningOfScreen();
					break;
				case 2:
					EraseScreen();
					break;
			}
		}
		else if (c == 'K')
		{
			int n = GetANSIValue(1);
			switch (n)
			{
				case 0:
					EraseToEndOfLine();
					break;
				case 1:
					EraseToBeginningOfLine();
					break;
				case 2:
					EraseLine();
					break;
			}
		}
		else if (c == 'L')
		{
			int n = GetANSIValue(1);
			if (n == 0)
			{
				n = 1;
			}
			for (int i = 0; i < n; i++)
			{
				InsertLine();
			}
		}
		else if (c == 'M')
		{
			int n = GetANSIValue(1);
			if (n == 0)
			{
				n = 1;
			}
			for (int i = 0; i < n; i++)
			{
				DeleteLine();
			}
		}
		else if (c == 'P')
		{
			int n = GetANSIValue(1);
			if (n == 0)
			{
				n = 1;
			}
			for (int i = 0; i < n; i++)
			{
				DeleteChar();
			}
		}
		else if (c == 'h')
		{
			if (ansiSeq[1] == '4')
			{
				EnterInsertCharMode();
			}
			else if (ansiSeq[1] == '>')
			{
				// set mode
				int n = GetANSIValue(2);
				switch (n)
				{
					case 1:
						Enable25();
						break;
					case 2:
						enableKeyClick = false;
						break;
					case 3:
						// enter hold screen mode
						break;
					case 4:
						enableBlockCursor = true;
						break;
					case 5:
						enableCursor = false;
						break;
					case 6:
						keypadShifted = true;
						break;
					case 7:
						altKeypadMode = true;
						break;
					case 8:
						enableLFonCR = true;
						break;
					case 9:
						enableCRonLF = true;
						break;
				}
			}
			else if (ansiSeq[1] == '?')
			{
				if (ansiSeq[2] == '7')
				{
					enableWrapAround = true;
				}
			}
		}
		else if (c == 'l')
		{
			if (ansiSeq[1] == '4')
			{
				ExitInsertCharMode();
			}
			else if (ansiSeq[1] == '>')
			{
				// reset mode
				int n = GetANSIValue(2);
				switch (n)
				{
					case 1:
						Disable25();
						break;
					case 2:
						enableKeyClick = true;
						break;
					case 3:
						// exit hold screen mode
						break;
					case 4:
						enableBlockCursor = false;
						break;
					case 5:
						enableCursor = true;
						break;
					case 6:
						keypadShifted = false;
						break;
					case 7:
						altKeypadMode = false;
						break;
					case 8:
						enableLFonCR = false;
						break;
					case 9:
						enableCRonLF = false;
						break;
				}
			}
			else if (ansiSeq[1] == '?')
			{
				if (ansiSeq[2] == '2')
				{
					enableANSIMode = false;
				}
				else if (ansiSeq[2] == '7')
				{
					enableWrapAround = false;
				}
			}
		}
		else if (c == 'z')
		{
			ResetToPowerUpConfig();
		}
		else if (c == 'm')
		{
			if (escSeqIndex == 1)
			{
				reverse = false;
			}
			else
			{
				int n = GetANSIValue(1);
				switch (n)
				{
					case 0:
						reverse = false;
						break;
					case 7:
						reverse = true;
						break;
					case 10:
						graphMode = true;
						break;
					case 11:
						graphMode = false;
						break;
				}
			}
		}
		else if (c == 'q')
		{
			// xmit 25th line
			XmitLine25();
		}
		else if (c == 'p')
		{
			// xmit page
			XmitPage();
		}
	}

	void DirectCursorAddress(byte c)
	{
		if (dcaIndex == 0)
		{
			line = c - 32;
			dcaIndex++;
		}
		else
		{
			column = c - 32;
			escSeq = false;
			dca = false;
		}
	}

	void SetSpecialMode(byte c)
	{
		if (c == '1')
		{
			enableLine25 = (resetMode) ? false : true;
			if (enableLine25)
			{
				Enable25();
			}
			else
			{
				Disable25();
			}
		}
		else if (c == '2')
		{
			// no key click
			enableKeyClick = (resetMode) ? true : false;
		}
		else if (c == '3')
		{
			// hold screen mode
		}
		else if (c == '4')
		{
			// block cursor
			enableBlockCursor = (resetMode) ? false : true;
		}
		else if (c == '5')
		{
			// cursor off
			enableCursor = (resetMode) ? true : false;
		}
		else if (c == '6')
		{
			// keypad shifted
			keypadShifted = (resetMode) ? false : true;
		}
		else if (c == '7')
		{
			// alternate keypad mode
			altKeypadMode = (resetMode) ? false : true;
		}
		else if (c == '8')
		{
			// auto linefeed on CR
			enableLFonCR = (resetMode) ? false : true;
		}
		else if (c == '9')
		{
			// auto CR on linefeed
			enableCRonLF = (resetMode) ? false : true;
		}
	}

	// scroll text down
	void ScrollDown()
	{
		int end = 23;
		int beg = 0;
		for (int i = end; i > beg; i--)
		{
			int n = i - 1;
			if (n >= 0)
			{
				int idx1 = n * 80;
				int idx2 = i * 80;
				for (int j = 0; j < 80; j++)
				{
					vidmem[idx2 + j] = vidmem[idx1 + j];
				}
			}
		}

		int idx = 0;
		for (int i = 0; i < 80; i++)
		{
			vidmem[idx + i] = 0x20;
		}
	}

	// scroll text up
	void ScrollUp()
	{
		int end = 23;
		int beg = 0;
		for (int i = beg; i < end; i++)
		{
			int n = i + 1;
			if (n < 24)
			{
				int idx1 = n * 80;
				int idx2 = i * 80;
				for (int j = 0; j < 80; j++)
				{
					vidmem[idx2 + j] = vidmem[idx1 + j];
				}
			}
		}

		int idx = 23 * 80;
		for (int i = 0; i < 80; i++)
		{
			vidmem[idx + i] = 0x20;
		}
	}

	void PutDebugLine(int i, string s)
	{
		int n = i * 80;
		for (int j = 0; j < s.Length; j++)
		{
			vidmem[n + j] = (byte)s[j];
		}
	}

	void SetCursorHome()
	{
		line = 0;
		column = 0;
	}

	void SetCursorForward()
	{
		column = Mathf.Min(79, column + 1);
	}

	void SetCursorBackward()
	{
		column = Mathf.Max(0, column - 1);
	}

	void SetCursorDown()
	{
		line = Mathf.Min(23, line + 1);
	}

	void SetCursorUp()
	{
		line = Mathf.Max(0, line - 1);
	}

	void SaveCursorPosition()
	{
		savePos = new Vector3(column, line, 0);
	}

	void RestoreCursorPosition()
	{
		column = (int)savePos.x;
		line = (int)savePos.y;
	}

	void SetCursorAddress(int x, int y)
	{
		line = y;
		column = x;
	}

	void EraseSection(int beg, int end)
	{
		for (int i = beg; i <= end; i++)
		{
			vidmem[i] = 0x20;
		}
	}

	void EraseScreen()
	{
		if (line == 24)
		{
			int beg = 24 * 80;
			int end = beg + 80;
			EraseSection(beg, end - 1);
			column = 0;
		}
		else
		{
			int end = 24 * 80;
			EraseSection(0, end - 1);
			SetCursorHome();
		}
	}

	void EraseToBeginningOfScreen()
	{
		if (line == 24)
		{
			EraseToBeginningOfLine();
		}
		else
		{
			int end = (line * 80) + column;
			EraseSection(0, end);
		}
	}

	void EraseToEndOfScreen()
	{
		if (line == 24)
		{
			EraseToEndOfLine();
		}
		else
		{
			int beg = (line * 80) + column;
			int end = 24 * 80;
			EraseSection(beg, end - 1);
		}
	}

	void EraseLine()
	{
		int beg = line * 80;
		int end = beg + 80;
		EraseSection(beg, end - 1);
	}

	void EraseToBeginningOfLine()
	{
		int end = (line * 80) + column;
		int beg = (line * 80);
		EraseSection(beg, end);
	}

	void EraseToEndOfLine()
	{
		int end = (line * 80) + 80;
		int beg = (line * 80) + column;
		EraseSection(beg, end - 1);
	}

	void InsertLine()
	{
		int end = 23;
		int beg = line;
		for (int i = end; i > beg; i--)
		{
			int n = i - 1;
			if (n >= 0)
			{
				int idx1 = n * 80;
				int idx2 = i * 80;
				for (int j = 0; j < 80; j++)
				{
					vidmem[idx2 + j] = vidmem[idx1 + j];
				}
			}
		}

		int idx = line * 80;
		for (int i = 0; i < 80; i++)
		{
			vidmem[idx + i] = 0x20;
		}

		column = 0;
	}

	void DeleteLine()
	{
		int end = 23;
		int beg = line;
		for (int i = beg; i < end; i++)
		{
			int n = i + 1;
			if (n < 24)
			{
				int idx1 = n * 80;
				int idx2 = i * 80;
				for (int j = 0; j < 80; j++)
				{
					vidmem[idx2 + j] = vidmem[idx1 + j];
				}
			}
		}

		int idx = 23 * 80;
		for (int i = 0; i < 80; i++)
		{
			vidmem[idx + i] = 0x20;
		}

		column = 0;
	}

	void DeleteChar()
	{
		int idx1 = (line * 80);
		for (int i = column; i < 80; i++)
		{
			int n = idx1 + i;
			if (((n + 1) % 80) != 0)
			{
				vidmem[n] = vidmem[n + 1];
			}
		}
		vidmem[idx1 + 79] = 0x20;
	}

	void EnterInsertCharMode()
	{
		enableInsertChar = true;
	}

	void ExitInsertCharMode()
	{
		enableInsertChar = false;
	}

	void XmitLine25()
	{
		int n = 24 * 80;
		for (int i = 0; i < 80; i++)
		{
			OutChar(vidmem[n]);
			n++;
		}
	}

	void XmitPage()
	{
		int end = 24 * 80;
		for (int i = 0; i < end; i++)
		{
			OutChar(vidmem[i]);
		}
	}

	void ResetToPowerUpConfig()
	{
		SetDefaults();
	}

	void RenderScreen()
	{
		ShowCursorTimer();

		if (dirty)
		{
			for (int i = 0; i < vidmem.Length; i++)
			{
				int x = i % 80;
				int y = i / 80;
				byte c = vidmem[i];
				RenderChar(x, y, c);
			}

			ShowCursor();

			h19ScreenTexture.Apply();

			dirty = false;
		}
	}

	void RenderChar(int x, int y, byte c)
	{
		Texture2D tex = h19Font.fontCharArray[c];

		int yPos = y * 10;
		for (int j = 0; j < 10; j++)
		{
			int xPos = x * 8;
			for (int i = 0; i < 8; i++)
			{
				Color color = tex.GetPixel(i, j);
				if (color != Color.black)
				{
					h19ScreenTexture.SetPixel(xPos, yPos, screenColor);
				}
				else
				{
					h19ScreenTexture.SetPixel(xPos, yPos, Color.black);
				}
				xPos++;
			}
			yPos++;
		}
	}

	void RenderCharAdd(int x, int y, byte c)
	{
		Texture2D tex = h19Font.fontCharArray[c];

		int yPos = y * 10;
		for (int j = 0; j < 10; j++)
		{
			int xPos = x * 8;
			for (int i = 0; i < 8; i++)
			{
				Color color = tex.GetPixel(i, j);
				if (color != Color.black)
				{
					h19ScreenTexture.SetPixel(xPos, yPos, screenColor);
				}
				xPos++;
			}
			yPos++;
		}
	}

	void Enable25()
	{
		enableLine25 = true;
	}

	void Disable25()
	{
		int y = 24 * 80;
		for (int i = 0; i < 80; i++)
		{
			int n = y + i;
			vidmem[n] = 0x20;
		}
		enableLine25 = false;
	}

	void ShowCursorTimer()
	{
		if (enableCursor)
		{
			blinkTimer += Time.deltaTime;
			if (blinkTimer > 0.1f)
			{
				cursorVis = !cursorVis;
				blinkTimer = 0;
				dirty = true;
			}
		}
	}

	void ShowCursor()
	{
		if (enableBlockCursor)
		{
			cursorType = blockCursor;
		}
		else
		{
			cursorType = lineCursor;
		}

		if (cursorVis && enableCursor)
		{
			RenderCharAdd(column, line, (byte)cursorType);
		}
	}

	//
	public string comPortString;
	public int baudRate;

	public void ShowConfig()
	{
		//H89.Instance.canvasRoot.SetActive(false);
		configScreen.gameObject.SetActive(true);
		h19ScreenRenderer.gameObject.SetActive(false);
		SetBaud();
	}

	public void HideConfig()
	{
		configScreen.gameObject.SetActive(false);
		h19ScreenRenderer.gameObject.SetActive(true);

		// 0=underscore (0)/block cursor (1)
		// 1=key click (0)/no key click (1)
		// 2=discard past end of line/wrap around
		// 3=no auto-lf on cr/auto-lf on cr
		// 4=no auto-cr on lf/auto-cr on lf
		// 5=heath mode/ansi mode
		// 6=keypad normal/keypad shifted
		// 7=60hz/50hz
		if (S402[0] != 0)
		{
			SetSpecialMode((byte)'4');
		}
		if (S402[1] != 0)
		{
			SetSpecialMode((byte)'2');
		}
	}

	void ShowConfigSettings()
	{
		if (S401[4] != 0)
		{
			s401_5_result.text = "PARITY";
			if (S401[5] == 0)
			{
				s401_6_result.text = "ODD Parity";
			}
			else
			{
				s401_6_result.text = "EVEN Parity";
			}
			if (S401[6] == 0)
			{
				s401_7_result.text = "NORMAL Parity";
			}
			else
			{
				s401_7_result.text = "STICK Parity";
			}
		}
		else
		{
			s401_5_result.text = "NO PARITY";
			s401_6_result.text = "N/A";
			s401_7_result.text = "N/A";
		}

		if (S401[7] == 0)
		{
			s401_8_result.text = "HALF DUPLEX";
		}
		else
		{
			s401_8_result.text = "FULL DUPLEX";
		}

		if (S402[0] == 0)
		{
			s402_1_result.text = "UNDERSCORE Cursor";
		}
		else
		{
			s402_1_result.text = "BLOCK Cursor";
		}

		if (S402[1] == 0)
		{
			s402_2_result.text = "KEYCLICK";
		}
		else
		{
			s402_2_result.text = "NO KEYCLICK";
		}

		if (S402[2] == 0)
		{
			s402_3_result.text = "NO WRAP AROUND";
		}
		else
		{
			s402_3_result.text = "WRAP AROUND";
		}

		if (S402[3] == 0)
		{
			s402_4_result.text = "NO LF ON CR";
		}
		else
		{
			s402_4_result.text = "LF ON CR";
		}

		if (S402[4] == 0)
		{
			s402_5_result.text = "NO CR ON LF";
		}
		else
		{
			s402_5_result.text = "CR ON LF";
		}

		if (S402[5] == 0)
		{
			s402_6_result.text = "HEATH MODE";
		}
		else
		{
			s402_6_result.text = "ANSI MODE";
		}

		if (S402[6] == 0)
		{
			s402_7_result.text = "KEYPAD NORMAL";
		}
		else
		{
			s402_7_result.text = "KEYPAD SHIFTED";
		}

		if (S402[7] == 0)
		{
			s402_8_result.text = "60 HZ";
		}
		else
		{
			s402_8_result.text = "50 HZ";
		}

		if (autoRepeatDelay.value == 0)
		{
			autoRepeatDelayValue.text = "OFF";
		}
		else
		{
			float v1 = autoRepeatDelay.value / 30;
			autoRepeatDelayValue.text = v1.ToString("F2") + "/secs";
		}
		float v2 = autoRepeatRate.value;
		autoRepeatRateValue.text = v2.ToString() + "/sec";
	}

	public void ColorGreenSelected()
	{
		if (colorGreen.isOn)
		{
			colorAmber.isOn = false;
			colorWhite.isOn = false;
			screenColor = screenColorGreen;
			screenColorIndex = 0;
		}
	}

	public void ColorAmberSelected()
	{
		if (colorAmber.isOn)
		{
			colorGreen.isOn = false;
			colorWhite.isOn = false;
			screenColor = screenColorAmber;
			screenColorIndex = 1;
		}
	}

	public void ColorWhiteSelected()
	{
		if (colorWhite.isOn)
		{
			colorGreen.isOn = false;
			colorAmber.isOn = false;
			screenColor = screenColorWhite;
			screenColorIndex = 2;
		}
	}

	void SaveConfig()
	{
		for (int i = 0; i < S401.Length; i++)
		{
			string key = "S401_" + i.ToString();
			PlayerPrefs.SetInt(key, S401[i]);
		}

		for (int i = 0; i < S402.Length; i++)
		{
			string key = "S402_" + i.ToString();
			PlayerPrefs.SetInt(key, S402[i]);
		}
		PlayerPrefs.SetInt("screencolor", screenColorIndex);
		PlayerPrefs.SetFloat("autodelay", autoRepeatDelay.value);
		PlayerPrefs.SetFloat("autorate", autoRepeatRate.value);
		PlayerPrefs.SetInt("baudrate", baudRate);
	}

	void SaveComSettings()
	{
		PlayerPrefs.SetString("comport", comPortString);
	}

	void LoadConfig()
	{
		skipSetBaud = true;
		for (int i = 0; i < S401.Length; i++)
		{
			string key = "S401_" + i.ToString();
			if (PlayerPrefs.HasKey(key))
			{
				S401[i] = (byte)PlayerPrefs.GetInt(key);
			}
		}

		s401_1.value = S401[0];
		s401_2.value = S401[1];
		s401_3.value = S401[2];
		s401_4.value = S401[3];
		s401_5.value = S401[4];
		s401_6.value = S401[5];
		s401_7.value = S401[6];
		s401_8.value = S401[7];

		for (int i = 0; i < S402.Length; i++)
		{
			string key = "S402_" + i.ToString();
			S402[i] = (byte)PlayerPrefs.GetInt(key);
		}

		s402_1.value = S402[0];
		s402_2.value = S402[1];
		s402_3.value = S402[2];
		s402_4.value = S402[3];
		s402_5.value = S402[4];
		s402_6.value = S402[5];
		s402_7.value = S402[6];
		s402_8.value = S402[7];

		if (PlayerPrefs.HasKey("comport"))
		{
			string savedComPort = PlayerPrefs.GetString("comport");
			for (int i = 0; i < comDropDown.options.Count; i++)
			{
				if (comDropDown.options[i].text.Equals(savedComPort))
				{
					//comDropDown.captionText.text = savedComPort;
					comDropDown.value = i;
					break;
				}
			}
		}

		SetCOMPort();
		//SetBaudFromSwitches();

		baudRate = PlayerPrefs.GetInt("baudrate");
		Debug.Log("baudRate=" + baudRate.ToString());
		if (baudRate == 0)
		{
			baudRate = 9600;
		}
		SetBaudDropDownValue();

		if (PlayerPrefs.HasKey("screencolor"))
		{
			screenColorIndex = PlayerPrefs.GetInt("screencolor");

			Debug.Log("screenColorIndex=" + screenColorIndex.ToString());

			if (screenColorIndex == 1)
			{
				colorAmber.isOn = true;
			}
			else if (screenColorIndex == 2)
			{
				colorWhite.isOn = true;
			}
			else
			{
				colorGreen.isOn = true;
			}
		}

		if (PlayerPrefs.HasKey("autodelay"))
		{
			autoRepeatDelay.value = PlayerPrefs.GetFloat("autodelay");
		}
		if (PlayerPrefs.HasKey("autorate"))
		{
			autoRepeatRate.value = PlayerPrefs.GetFloat("autorate");
		}
		skipSetBaud = false;
	}

	public void SetBaud()
	{
		baudRate = int.Parse(baudDropDown.captionText.text);
		SetBaudDipSwitches();
	}

	void SetBaudDipSwitches()
	{
		switch (baudRate)
		{
			case 300:
				s401_1.value = 1;
				s401_2.value = 1;
				s401_3.value = 0;
				s401_4.value = 0;
				break;
			case 600:
				s401_1.value = 0;
				s401_2.value = 0;
				s401_3.value = 1;
				s401_4.value = 0;
				break;
			case 1200:
				s401_1.value = 1;
				s401_2.value = 0;
				s401_3.value = 1;
				s401_4.value = 0;
				break;
			case 2400:
				s401_1.value = 0;
				s401_2.value = 0;
				s401_3.value = 0;
				s401_4.value = 1;
				break;
			case 4800:
				s401_1.value = 0;
				s401_2.value = 1;
				s401_3.value = 0;
				s401_4.value = 1;
				break;
			case 9600:
				s401_1.value = 0;
				s401_2.value = 0;
				s401_3.value = 1;
				s401_4.value = 1;
				break;
			case 19200:
				s401_1.value = 1;
				s401_2.value = 0;
				s401_3.value = 1;
				s401_4.value = 1;
				break;
			case 38400:
				s401_1.value = 0;
				s401_2.value = 1;
				s401_3.value = 1;
				s401_4.value = 1;
				break;
			default:
				s401_1.value = 1;
				s401_2.value = 1;
				s401_3.value = 1;
				s401_4.value = 1;
				break;
		}
		S401[0] = (byte)s401_1.value;
		S401[1] = (byte)s401_2.value;
		S401[2] = (byte)s401_3.value;
		S401[3] = (byte)s401_4.value;
	}

	private bool skipSetBaud;

	public void SetBaudFromSwitches()
	{
		if (skipSetBaud)
		{
			return;
		}

		S401[0] = (byte)s401_1.value;
		S401[1] = (byte)s401_2.value;
		S401[2] = (byte)s401_3.value;
		S401[3] = (byte)s401_4.value;

		int baud = 0;
		
		int b = S401[0] | (S401[1] << 1) | (S401[2] << 2) | (S401[3] << 3);
		switch (b)
		{
			case 0x00:
				baud = 110;
				break;
			case 0x01:
				baud = 110;
				break;
			case 0x02:
				baud = 150;
				break;
			case 0x03:
				baud = 300;
				break;
			case 0x04:
				baud = 600;
				break;
			case 0x05:
				baud = 1200;
				break;
			case 0x06:
				baud = 1800;
				break;
			case 0x07:
				baud = 2000;
				break;
			case 0x08:
				baud = 2400;
				break;
			case 0x09:
				baud = 3600;
				break;
			case 0x0A:
				baud = 4800;
				break;
			case 0x0B:
				baud = 7200;
				break;
			case 0x0C:
				baud = 9600;
				break;
			case 0x0D:
				baud = 19200;
				break;
			case 0x0E:
				baud = 38400;
				break;
			case 0x0F:
				baud = 57600;
				break;
			default:
				baud = 9600;
				break;
		}
		
		baudRate = baud;
		SetBaudDropDownValue();
	}

	void SetBaudDropDownValue()
	{
		skipSetBaud = true;
		for (int i = 0; i < baudDropDown.options.Count; i++)
		{
			int v = int.Parse(baudDropDown.options[i].text);
			Debug.Log("baudDropDown " + i.ToString() + "=" + v.ToString() + " baudRate=" + baudRate.ToString());
			if (baudRate == v)
			{
				baudDropDown.value = i;
				break;
			}
		}
		skipSetBaud = false;
	}

	public void SetCOMPort()
	{
		if (string.IsNullOrEmpty(comDropDown.captionText.text))
		{
			return;
		}
		comPortString = comDropDown.captionText.text;
	}

	void InitCOMPortItems()
	{
#if UNITY_IOS
		useSerialPort = false;
#endif
		if (useSerialPort)
		{
			string[] comPortNames = SerialPort.GetPortNames();
			List<string> comPortNamesList = new List<string>();
			//comPortNamesList.Add("H89EMU");
			if (comPortNames != null && comPortNames.Length > 0)
			{
				for (int i = 0; i < comPortNames.Length; i++)
				{
					comPortNamesList.Add(comPortNames[i]);
				}
				//List<string> comPortNamesList = new List<string>(comPortNames);
			}
			comDropDown.AddOptions(comPortNamesList);
		}
		else
		{
			List<string> comPortNamesList = new List<string>();
			//comPortNamesList.Add("H89EMU");
			comDropDown.AddOptions(comPortNamesList);
		}
	}

	public void Connect()
	{
		//SetupSerialPort();
		if (useSerialPort)
		{
			if (serialPort != null)
			{
				if (serialPort.IsOpen)
				{
					serialPort.Close();
				}
				serialPort = null;
			}
		}

		offline = offlineCheckbox.isOn;

		if (offline)
		{
			// do nothing
		}
		else
		{
			comPortString = comDropDown.captionText.text;
			int baud = baudRate;

			Parity parity = Parity.None;
			
			if (s401_5.value != 0)
			{
				if (s401_6.value == 0)
				{
					parity = Parity.Odd;
				}
				else
				{
					parity = Parity.Even;
				}
				if (s401_7.value == 0)
				{
					parity = Parity.Mark;
				}
				else
				{
					parity = Parity.Space;
				}
			}

			if (comPortString.Contains("H89EMU"))
			{
				useSerialPort = false;
				if (onConnect != null)
				{
					onConnect();
				}
			}
			else
			{
				useSerialPort = true;
				if (useSerialPort)
				{
					serialPort = new SerialPort(comPortString, baud, parity, 8, StopBits.One);
					serialPort.Handshake = Handshake.None;

					Debug.Log("serialPort.Open() " + serialPort.PortName + " baud=" + baud.ToString());

					try
					{
						serialPort.Open();
						serialPort.RtsEnable = true;
						serialPort.DtrEnable = true;

						SaveComSettings();

						Debug.Log("SerialPort open");
					}
					catch
					{
						Debug.Log("No serial port found");
						offline = true;
					}
				}
			}
		}

		SaveConfig();
		HideConfig();
	}

	/*
	void SetupSerialPort()
	{
		if (serialPort != null)
		{
			if (serialPort.IsOpen)
			{
				serialPort.Close();
			}
			serialPort = null;
		}

		int baud = 0;
		int b = S401[0] | (S401[1] << 1) | (S401[2] << 2) | (S401[3] << 3);
		switch (b)
		{
			case 0x01:
				baud = 110;
				break;
			case 0x02:
				baud = 150;
				break;
			case 0x03:
				baud = 300;
				break;
			case 0x04:
				baud = 600;
				break;
			case 0x05:
				baud = 1200;
				break;
			case 0x06:
				baud = 1800;
				break;
			case 0x07:
				baud = 2000;
				break;
			case 0x08:
				baud = 2400;
				break;
			case 0x09:
				baud = 3600;
				break;
			case 0x0A:
				baud = 4800;
				break;
			case 0x0B:
				baud = 7200;
				break;
			case 0x0C:
				baud = 9600;
				break;
			case 0x0D:
				baud = 19200;
				break;
			case 0x0E:
				baud = 38400;
				break;
			case 0x0F:
				baud = 57600;
				break;
			default:
				baud = 9600;
				break;
		}

		serialPort = new SerialPort("COM6", baud, Parity.None, 8, StopBits.One);
		//serialPort.BaudRate = baud;
		//serialPort.Parity = Parity.None;
		//serialPort.DataBits = 8;
		//serialPort.StopBits = StopBits.One;
		serialPort.Handshake = Handshake.None; // Handshake.RequestToSend;

		comPortString = serialPort.PortName;
		baudRate = serialPort.BaudRate;

		Debug.Log("serialPort.Open() " + serialPort.PortName + " baud=" + baud.ToString());

		try
		{
			serialPort.Open();
			//serialPort.DiscardInBuffer();
			Debug.Log("SerialPort open");
		}
		catch
		{
			Debug.Log("No serial port found");
			offline = true;
		}
	}
	*/

	void HandleSerialPort()
	{
		if (offline)
		{
			if (writeIndex > 0)
			{
				for (int i = 0; i < writeIndex; i++)
				{
					InChar(writeBuffer[i]);
				}
				writeIndex = 0;
			}
			return;
		}
		if (useSerialPort)
		{
			if (serialPort != null && serialPort.IsOpen)
			{
				int bytes = serialPort.BytesToRead;
				if (bytes > 0)
				{
					bytes = Mathf.Min(bytes, readBuffer.Length);
					serialPort.Read(readBuffer, 0, bytes);
					for (int i = 0; i < bytes; i++)
					{
						byte b = readBuffer[i];

						//Debug.Log("In " + b.ToString());

						InChar(b);
					}
				}

				if (writeIndex > 0)
				{
					if (MCZImager.sendImage)
					{
						// do not send the char to the output port
					}
					else if (!disableOutput)
					{
						serialPort.Write(writeBuffer, 0, writeIndex);
					}
					writeIndex = 0;
				}
			}
		}
		else
		{
			if (writeIndex > 0)
			{
				for (int i = 0; i < writeIndex; i++)
				{
					outCharBuffer[endOutCharIndex] = writeBuffer[i];
					endOutCharIndex = (endOutCharIndex + 1) % outCharBuffer.Length;
				}
				writeIndex = 0;
			}
		}
	}

	private byte[] outCharBuffer = new byte[256];
	private int begOutCharIndex;
	private int endOutCharIndex;

	public void InCharDirect(byte c)
	{
		InChar(c);
	}

	public byte GetCharDirect()
	{
		if (begOutCharIndex != endOutCharIndex)
		{
			byte c = outCharBuffer[begOutCharIndex];
			begOutCharIndex = (begOutCharIndex + 1) % outCharBuffer.Length;
			return c;
		}
		return 0;
	}

	public bool HasCharDirect()
	{
		if (begOutCharIndex != endOutCharIndex)
		{
			return true;
		}
		return false;
	}

	public void OutCharDirect(byte c)
	{
		OutChar(c);
	}

	private string logStr;

	public void LogByte(byte c)
	{
		if (char.IsLetterOrDigit((char)c))
		{
			// do nothing
		}
		else if (char.IsPunctuation((char)c))
		{
			// do nothing
		}
		else if (char.IsSymbol((char)c))
		{
			// do nothing
		}
		else if (c == 0x0D || c == 0x0A || c == 0x08 || c == 0x09 || c == 0x20 || c == 0)
		{
			// do nothing
		}
		else
		{
			c = (byte)'.';
		}
		logStr += (char)c;
	}

	public void LogToggle()
	{
		if (logToggle.isOn)
        {
			// toggled on
        }
		else
        {
			SaveLog();
        }
	}

	void SaveLog()
	{
		disableOutput = true;
		FilePicker.Instance.onCompleteCallback += OnSaveLogSuccess;
		FilePicker.Instance.ShowPicker(true);
		//SimpleFileBrowser.FileBrowser.ShowSaveDialog(OnSaveLogSuccess, OnSaveLogCancel, SimpleFileBrowser.FileBrowser.PickMode.Files);
	}

	void OnSaveLogSuccess(string filePath)
	{
		disableOutput = false;
		FilePicker.Instance.onCompleteCallback -= OnSaveLogSuccess;
		if (!string.IsNullOrEmpty(logStr))
		{
			System.IO.File.WriteAllText(filePath, logStr);
			logStr = string.Empty;

			Debug.Log("Log file saved to " + filePath);
		}
	}

	public static void ResetSystem()
	{
		Instance.ResetToPowerUpConfig();
		Instance.EraseScreen();
	}
}

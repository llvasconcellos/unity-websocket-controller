using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
using System.Threading;
using System.Linq;
using UnityEngine.UI;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Security.Cryptography;


//GameController handles threading
public class GameController : MonoBehaviour
{
    private static GameController _current;
    public Text countText;
    private TcpListener listener;
    public static String lixo;


    private String DecodeMessage(Byte[] bytes)
    {
        String incomingData = String.Empty;
        Byte secondByte = bytes[1];
        Int32 dataLength = secondByte & 127;
        Int32 indexFirstMask = 2;
        if (dataLength == 126)
            indexFirstMask = 4;
        else if (dataLength == 127)
            indexFirstMask = 10;

        IEnumerable<Byte> keys = bytes.Skip(indexFirstMask).Take(4);
        Int32 indexFirstDataByte = indexFirstMask + 4;

        Byte[] decoded = new Byte[bytes.Length - indexFirstDataByte];
        for (Int32 i = indexFirstDataByte, j = 0; i < bytes.Length; i++, j++)
        {
            decoded[j] = (Byte)(bytes[i] ^ keys.ElementAt(j % 4));
        }

        return incomingData = Encoding.UTF8.GetString(decoded, 0, decoded.Length);
    }

    void Start()
    {

        //TcpListener server = new TcpListener(IPAddress.Any, 2033);
        TcpListener server = new TcpListener(IPAddress.Parse("192.168.15.2"), 2033);

        server.Start();


        Debug.Log("Server has started on 127.0.0.1:80.{0}Waiting for a connection...");




        GameController.RunAsync(() => {

            TcpClient client = server.AcceptTcpClient();

            Debug.Log("A client connected.");
            qrCode.visible = false;

            NetworkStream stream = client.GetStream();

            //enter to an infinite cycle to be able to handle every change in stream
            bool handShaked = false;
            while (true)
            {
                while (!stream.DataAvailable) ;

                Byte[] bytes = new Byte[client.Available];

                stream.Read(bytes, 0, bytes.Length);

                //translate bytes of request to string
                String data = Encoding.UTF8.GetString(bytes);
                if (!handShaked)
                {
                    if (new Regex("^GET").IsMatch(data))
                    {
                        Byte[] response = Encoding.UTF8.GetBytes("HTTP/1.1 101 Switching Protocols" + Environment.NewLine
                            + "Connection: Upgrade" + Environment.NewLine
                            + "Upgrade: websocket" + Environment.NewLine
                            + "Sec-WebSocket-Accept: " + Convert.ToBase64String(
                                SHA1.Create().ComputeHash(
                                    Encoding.UTF8.GetBytes(
                                        new Regex("Sec-WebSocket-Key: (.*)").Match(data).Groups[1].Value.Trim() + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11"
                                    )
                                )
                            ) + Environment.NewLine
                            + Environment.NewLine);

                        stream.Write(response, 0, response.Length);
                    }
                    handShaked = true;
                }
                


                Debug.Log(data);


                data = DecodeMessage(bytes);

                PlayerController.moveVertical = 0f;
                PlayerController.moveHorizontal = 0f;


                if (data == "113") break;

                switch (data)
                {
                    case "65": //up
                        PlayerController.moveVertical = 0.5f;
                        break;
                    case "66": //down
                        PlayerController.moveVertical = -0.5f;
                        break;
                    case "67": //right
                        PlayerController.moveHorizontal = 0.5f;
                        break;
                    case "68": //left
                        PlayerController.moveHorizontal = -0.5f;
                        break;
                    case "120": //x
                        break;
                    case "122": //z
                        break;
                }

                GameController.lixo = data;
                Thread.Sleep(10);
                

            }


            

            



            //If you need to then call back to the game
            GameController.QueueOnMainThread(() => {
                //Code triggered from the other thread that
                //accesses Unity object goes here
            });
        });
    }

    private byte[] GetBytes(string str)
    {
        byte[] bytes = new byte[str.Length * sizeof(char)];
        System.Buffer.BlockCopy(str.ToCharArray(), 0, bytes, 0, bytes.Length);
        return bytes;
    }

    private string GetString(byte[] bytes)
    {
        char[] chars = new char[bytes.Length / sizeof(char)];
        System.Buffer.BlockCopy(bytes, 0, chars, 0, bytes.Length);
        return new string(chars);
    }

    public static GameController Current
    {
        get
        {
            if (_current == null && Application.isPlaying)
            {

                var g = GameObject.Find("GameController");
                if (g == null)
                {
                    g = new GameObject("GameController");
                    g.hideFlags = HideFlags.HideAndDontSave;
                }

                _current = g.GetComponent<GameController>() ?? g.AddComponent<GameController>();
            }

            return _current;
        }
    }

    void Awake()
    {
        if (_current != null && _current != this)
        {
            DestroyImmediate(gameObject);
        }
        else
        {
            _current = this;
        }
    }

    private List<Action> _actions = new List<Action>();
    public class DelayedQueueItem
    {
        public float time;
        public Action action;
        public string name;
    }
    private List<DelayedQueueItem> _delayed = new List<DelayedQueueItem>();

    public static void QueueOnMainThread(Action action, float time, string name)
    {
        lock (Current._delayed)
        {
            if (Current._delayed.Any(d => d.name == name))
                return;
            QueueOnMainThread(action, time);
        }
    }

    public static void QueueOnMainThread(Action action, string name)
    {
        QueueOnMainThread(action, 0, name);
    }

    public static void QueueOnMainThread(Action action, float time)
    {
        if (time != 0)
        {
            lock (Current._delayed)
            {
                Current._delayed.Add(new DelayedQueueItem { time = Time.time + time, action = action });
            }
        }
        else
        {
            lock (Current._actions)
            {
                Current._actions.Add(action);
            }
        }
    }

    public static void QueueOnMainThread(Action action)
    {
        lock (Current._actions)
        {
            Current._actions.Add(action);
        }
    }

    public static void RunAsync(Action a)
    {
        var t = new Thread(RunAction);
        t.Priority = System.Threading.ThreadPriority.Normal;
        t.Start(a);
    }

    private static void RunAction(object action)
    {
        ((Action)action)();
    }


    Action[] toBeRun = new Action[1000];
    DelayedQueueItem[] toBeDelayed = new DelayedQueueItem[1000];

    void Update()
    {
        try
        {
            var actions = 0;
            var delayedCount = 0;
            //Process the non-delayed actions
            lock (_actions)
            {
                for (var i = 0; i < _actions.Count; i++)
                {
                    toBeRun[actions++] = _actions[i];
                    if (actions == 999)
                        break;
                }
                _actions.Clear();
            }
            for (var i = 0; i < actions; i++)
            {
                var a = toBeRun[i];
                try
                {
                    a();
                }
                catch (Exception e)
                {
                    Debug.LogError("Queued Exception: " + e.ToString());
                }
            }
            lock (_delayed)
            {
                for (var i = 0; i < _delayed.Count; i++)
                {
                    var d = _delayed[i];
                    if (d.time < Time.time)
                    {
                        toBeDelayed[delayedCount++] = d;
                        if (delayedCount == 999)
                            break;
                    }
                }
            }
            for (var i = 0; i < delayedCount; i++)
            {
                var delayed = toBeDelayed[i];
                lock (_delayed)
                {
                    _delayed.Remove(delayed);
                }
                try
                {
                    delayed.action();
                }
                catch (Exception e)
                {
                    Debug.LogError("Delayed Exception:" + e.ToString());
                }
            }

        }
        catch (Exception e)
        {
            Debug.LogError("GameController Error " + e.ToString());
        }
    }
}
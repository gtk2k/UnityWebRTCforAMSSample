using Newtonsoft.Json;
using System;
using System.Linq;
using System.Security.Authentication;
using System.Threading;
using UnityEngine;
using WebSocketSharp;

public class AntMediaSignaling
{
    public delegate void dlgOnOpen();
    public delegate void dlgOnClose();
    public delegate void dlgOnWSError(string errorMessage);
    public delegate void dlgOnSignalingError(string errorMessage);
    public delegate void dlgOnStart(string streamId);
    public delegate void dlgOnOffer(AntMediaSignalingMessage msg);
    public delegate void dlgOnAnswer(AntMediaSignalingMessage msg);
    public delegate void dlgOnIceCandidate(AntMediaSignalingMessage msg);

    public event dlgOnOpen OnOpen;
    public event dlgOnClose OnClose;
    public event dlgOnWSError OnWSError;
    public event dlgOnSignalingError OnSignalingError;
    public event dlgOnStart OnStart;
    public event dlgOnOffer OnOffer;
    public event dlgOnAnswer OnAnswer;
    public event dlgOnIceCandidate OnIceCandidate;

    private WebSocket ws;
    private enum SslProtocolsHack
    {
        Tls = 192,
        Tls11 = 768,
        Tls12 = 3072
    }
    private SslProtocols sslProtocolHack = (SslProtocols)(SslProtocolsHack.Tls12 | SslProtocolsHack.Tls11 | SslProtocolsHack.Tls);

    private SynchronizationContext context;
    private JsonSerializerSettings jsonSettings;

    public AntMediaSignaling(string url)
    {
        jsonSettings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore
        };
        context = SynchronizationContext.Current;
        ws = new WebSocket(url);
        ws.SslConfiguration.EnabledSslProtocols = sslProtocolHack;

        ws.OnOpen += (s, e) =>
        {
            context.Post(_ =>
            {
                OnOpen.Invoke();
            }, null);
        };
        ws.OnMessage += (s, e) =>
        {
            context.Post(_ =>
            {
                    //log.AppendLine($"({e.Data})");
                    var msg = JsonUtility.FromJson<AntMediaSignalingMessage>(e.Data);
                ProcessMessage(msg);
            }, null);
        };
        ws.OnClose += (s, e) =>
        {
            context.Post(_ =>
            {
                OnClose.Invoke();
            }, null);
        };
        ws.OnError += (s, e) =>
        {
            context.Post(_ =>
            {
                OnWSError.Invoke(e.Message);
            }, null);
        };
    }

    public void Connect()
    {
        try
        {
            ws.Connect();
        }
        catch (Exception ex)
        {
            Debug.LogError(ex.Message);
        }
    }

    public void Close()
    {
        ws.Close();
    }

    private void ProcessMessage(AntMediaSignalingMessage msg)
    {
        switch (msg.command)
        {
            case "start":
                OnStart.Invoke(msg.streamId);
                break;
            case "takeConfiguration":
                if (msg.type == "offer")
                {
                    OnOffer.Invoke(msg);
                }
                else if (msg.type == "answer")
                {
                    OnAnswer.Invoke(msg);
                }
                break;
            case "takeCandidate":
                OnIceCandidate.Invoke(msg);
                break;
            case "error":
                OnSignalingError.Invoke(msg.definition);
                break;
        }
    }

    public void Publish(string streamId)
    {
        var msg = new AntMediaSignalingMessage
        {
            command = "publish",
            streamId = streamId,
            video = true,
            audio = false
        };
        send(msg);
    }

    public void Play(string streamId)
    {
        var msg = new AntMediaSignalingMessage
        {
            command = "play",
            streamId = streamId
        };
        send(msg);
    }

    public void SendDesc(string streamId, string type, string sdp)
    {
        var msg = new AntMediaSignalingMessage
        {
            command = "takeConfiguration",
            streamId = streamId,
            type = type,
            sdp = sdp
        };
        send(msg);
    }

    public void SendIceCandidate(string streamId, string candidate, int sdpMLineIndex, string sdpMId)
    {
        var msg = new AntMediaSignalingMessage
        {
            command = "takeCandidate",
            streamId = streamId,
            label = sdpMLineIndex,
            id = sdpMId,
            candidate = candidate
        };
        send(msg);
    }

    public void Stop(string streamId)
    {
        var msg = new AntMediaSignalingMessage
        {
            command = "stop",
            streamId = streamId
        };
        send(msg);
    }

    private void send(AntMediaSignalingMessage msg)
    {
        var json = JsonConvert.SerializeObject(msg, Formatting.Indented, jsonSettings);
        var logJson = string.Join("\n", json.Split('\n').Select(x => $"> {x}"));
        ws.Send(json);
    }
}

public class AntMediaSignalingMessage
{
    public string command;
    public string streamId;
    public string token;
    public string type; // "offer" / "answer"
    public string sdp; // sdp
    public int? label; // sdpMLineIndex
    public string id; // sdpMid
    public string candidate; // candidate
    public string room;
    public string definition;
    public string[] streams;
    public bool video;
    public bool audio;
}

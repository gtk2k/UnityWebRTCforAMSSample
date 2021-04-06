using System;
using System.Collections;
using System.Linq;
using System.Text;
using Unity.WebRTC;
using UnityEngine;
using UnityEngine.Video;

public class AntMediaServerUnityWebRTCClient : MonoBehaviour
{
    public ClientType clientType;
    public string streamId;
    public string signalingUrl;
    public RTCIceServer[] iceServers;
    public string dataChannelLabel = "data-channel";
    //public VideoStreamSource videoStreamSource;
    //public Camera camera;
    public VideoPlayer videoPlayer;
    public GameObject playerDisplay;
    public int videoWidth = 1280;
    public int videoHeight = 720;
    public int videoBitrate = 1500;
    public LogLevel logLevel;

    private RTCPeerConnection peer;
    private RTCDataChannel dataChannel;
    private AntMediaSignaling signaling;

    private RTCOfferOptions offerOptions = new RTCOfferOptions
    {
        iceRestart = false,
        offerToReceiveAudio = true,
        offerToReceiveVideo = true
    };
    private RTCAnswerOptions answerOptions = new RTCAnswerOptions
    {
        iceRestart = false
    };

    public enum ClientType
    {
        Publisher,
        Player
    }

    public enum VideoStreamSource
    {
        Camera,
        VideoPlayer
    }

    public enum LogLevel
    {
        None,
        Log,
        Warning,
        Error
    }

    void Start()
    {
        WebRTC.Initialize(EncoderType.Software);

        if (string.IsNullOrEmpty(streamId.Trim()))
            streamId = Guid.NewGuid().ToString("N");

        if (clientType == ClientType.Publisher)
        {
            if (videoPlayer == null)
            {
                Debug.LogError("VideoPlayer is null");
                return;
            }

            var rt = new RenderTexture(videoWidth, videoHeight, 0, RenderTextureFormat.BGRA32);
            videoPlayer.renderMode = VideoRenderMode.RenderTexture;
            videoPlayer.targetTexture = rt;
            playerDisplay.GetComponent<Renderer>().material.mainTexture = rt;
            videoPlayer.Play();
        }

        StartCoroutine(WebRTC.Update());
        Connect();
    }

    private void OnDestroy()
    {
        peer?.Dispose();
        peer = null;

        WebRTC.Dispose();
    }

    public void Connect()
    {
        signaling = new AntMediaSignaling(signalingUrl);
        signaling.OnOpen += Signaling_OnOpen;
        signaling.OnIceCandidate += Signaling_OnIceCandidate;
        signaling.OnStart += Signaling_OnStart;
        signaling.OnOffer += Signaling_OnOffer;
        signaling.OnAnswer += Signaling_OnAnswer;
        signaling.OnClose += Signaling_OnClose;
        signaling.OnWSError += Signaling_OnWSError;
        signaling.OnSignalingError += Signaling_OnSignalingError;
        signaling.Connect();
    }

    private void Signaling_OnOpen()
    {
        log(LogLevel.Log, $"[Signaling OnOpen]");

        if (clientType == ClientType.Publisher)
        {
            log(LogLevel.Log, $">>> Send \"Publish\" Command (streamId: {streamId})");
            signaling.Publish(streamId);
        }
        else
        {
            log(LogLevel.Log, $">>> Send \"Play\" Command (streamId: {streamId}");
            signaling.Play(streamId);
        }
    }

    private void Signaling_OnStart(string streamId)
    {
        log(LogLevel.Log, $"[Signaling OnStart]");
        createPeer();
    }

    private void Signaling_OnIceCandidate(AntMediaSignalingMessage msg)
    {
        var candidate = new RTCIceCandidate(new RTCIceCandidateInit
        {
            candidate = msg.candidate,
            sdpMLineIndex = msg.label,
            sdpMid = msg.id
        });
        peer.AddIceCandidate(candidate);
    }

    private void Signaling_OnOffer(AntMediaSignalingMessage msg)
    {
        log(LogLevel.Log, $"[Signaling OnOffer]");
        createPeer();
        StartCoroutine(setRemoteDesc(RTCSdpType.Offer, msg.sdp));
    }

    private void Signaling_OnAnswer(AntMediaSignalingMessage msg)
    {
        log(LogLevel.Log, $"[Signaling OnAnswer]");
        StartCoroutine(setRemoteDesc(RTCSdpType.Answer, msg.sdp));
    }

    private void Signaling_OnClose()
    {
        log(LogLevel.Log, $"[Signaling OnClose]");
    }

    private void Signaling_OnWSError(string errorMessage)
    {
        log(LogLevel.Error, $"[Signaling OnWSError] {errorMessage}");
    }

    private void Signaling_OnSignalingError(string errorMessage)
    {
        log(LogLevel.Error, $"[Signaling OnSignalingError] {errorMessage}");
    }

    private void createPeer()
    {
        log(LogLevel.Log, "Create RTCPeerConnection");
        var peerConfig = new RTCConfiguration { iceServers = iceServers };
        peer = new RTCPeerConnection(ref peerConfig);
        peer.OnConnectionStateChange = connectionState =>
        {
            log(LogLevel.Log, $"[OnConnectionStateChange] connectionState: {connectionState}");
        };
        peer.OnDataChannel = channel =>
        {
            dataChannel = channel;
            setupDataChannelEventHandler();
            log(LogLevel.Log, $"[OnDataChannel] label: {channel.Label}");
        };
        peer.OnIceCandidate = candidate =>
        {
            log(LogLevel.Log, $"[OnIceCandidate]");
            log(LogLevel.Log, $">>> Send \"takeCandidate\" Command (iceCandidate: '{candidate.Candidate.Substring(0, 10)} ...')");
            signaling.SendIceCandidate(streamId, candidate.Candidate, candidate.SdpMLineIndex.Value, candidate.SdpMid);
        };
        peer.OnIceGatheringStateChange = state =>
        {
            log(LogLevel.Log, $"[OnIceGatheringStateChange] iceGatheringState: {state}");
        };
        peer.OnNegotiationNeeded = () =>
        {
            log(LogLevel.Log, $"[OnNegotiationNeeded]");
        };
        peer.OnTrack = evt =>
        {
            log(LogLevel.Log, $"[OnTrack] kind: {evt.Track.Kind}");
            if (evt.Track is VideoStreamTrack track)
            {
                var texture = track.InitializeReceiver(videoWidth, videoHeight);
                playerDisplay.GetComponent<Renderer>().material.mainTexture = texture;
            }
        };

        var dcOptions = new RTCDataChannelInit();
        log(LogLevel.Log, $"CreateDataChannel label: {dataChannelLabel}");
        dataChannel = peer.CreateDataChannel(dataChannelLabel, dcOptions);
        setupDataChannelEventHandler();
        if (clientType == ClientType.Publisher)
        {
            var videoTrack = new VideoStreamTrack("VideoTrack", videoPlayer.targetTexture);
            peer.AddTrack(videoTrack);
            StartCoroutine(createDesc(RTCSdpType.Offer));
        }
    }

    private IEnumerator createDesc(RTCSdpType type)
    {

        RTCSessionDescriptionAsyncOperation opCreate;
        if (type == RTCSdpType.Offer)
        {
            opCreate = peer.CreateOffer(ref offerOptions);
        }
        else
        {
            opCreate = peer.CreateAnswer(ref answerOptions);
        }

        yield return opCreate;
        if (opCreate.IsError)
        {
            log(LogLevel.Error, $"Create {opCreate.Desc.type}: {opCreate.Error.message}");
            yield break;
        }
        else
        {
            log(LogLevel.Log, $"Create {opCreate.Desc.type}");
        }

        if (opCreate.Desc.sdp == null)
        {
            log(LogLevel.Error, $"opCreate.Desc.sdp is null");
            yield break;
        }
        else
        {
            log(LogLevel.Log, $"SetLocalDescription {type}: {opCreate.Desc.sdp}");
        }
        var desc = opCreate.Desc;
        var opSet = peer.SetLocalDescription(ref desc);
        yield return opSet;
        if (opSet.IsError)
        {
            log(LogLevel.Error, $"SetLocalDescription {type}: {opSet.Error.message}");
            yield break;
        }

        log(LogLevel.Log, $">>> Send \"takeConfiguration\" Command ({type}: '{desc.sdp.Substring(0, 10)} ...')");
        signaling.SendDesc(streamId, desc.type.ToString().ToLower(), desc.sdp);
    }

    private IEnumerator setRemoteDesc(RTCSdpType type, string sdp)
    {
        var desc = new RTCSessionDescription
        {
            type = type,
            sdp = sdp
        };

        log(LogLevel.Log, $"SetRemoteDescription {type}");
        var opSetDesc = peer.SetRemoteDescription(ref desc);
        yield return opSetDesc;

        if (opSetDesc.IsError)
        {
            log(LogLevel.Error, $"SetRemoteDescription {type}: {opSetDesc.Error.message}");
            yield break;
        }

        if (type == RTCSdpType.Offer)
            yield return StartCoroutine(createDesc(RTCSdpType.Answer));
    }

    private void setupDataChannelEventHandler()
    {
        dataChannel.OnOpen = () =>
        {
            log(LogLevel.Log, $"DC_OnOpen");
        };
        dataChannel.OnMessage = evt =>
        {
            var msg = Encoding.UTF8.GetString(evt);
            log(LogLevel.Log, $"DC_OnMessage: \"{msg}\"");
        };
        dataChannel.OnClose = () =>
        {
            log(LogLevel.Log, $"DC_OnClose");
        };
    }

    private void log(LogLevel level, string msg)
    {
        var time = DateTime.Now.ToString("HH:mm:ss.fff");

        switch (level)
        {
            case LogLevel.Log:
                if ((int)logLevel == (int)LogLevel.Log)
                {

#if UNITY_EDITOR
                        Debug.Log(msg);
#else
                    if (Debug.isDebugBuild)
                        Debug.LogError($"{time} <Log> {msg}");
                    else
                        Debug.Log(msg);
#endif
                }
                break;
            case LogLevel.Warning:
                if ((int)logLevel <= (int)LogLevel.Warning)
                {
#if UNITY_EDITOR
                    Debug.LogWarning(msg);
#else
                    if (Debug.isDebugBuild)
                        Debug.LogError($"{time} <Log> {msg}");
                    else
                        Debug.LogWarning(msg);
#endif
                }
                break;
            case LogLevel.Error:
                if (logLevel <= LogLevel.Error)
                {
#if UNITY_EDITOR
                    Debug.LogError(msg);
#else
                    if (Debug.isDebugBuild)
                        Debug.LogError($"{time} <Log> {msg}");
                    else
                        Debug.LogError(msg);
#endif
                }
                break;
        }
    }

    void Update()
    {

    }
}


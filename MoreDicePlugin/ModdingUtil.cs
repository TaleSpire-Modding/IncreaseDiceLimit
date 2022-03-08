using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BepInEx;
using BepInEx.Logging;
using Bounce.Unmanaged;
using DataModel;
using Newtonsoft.Json;
using TMPro;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace IncreaseDiceLimit
{
    public class PlayerControlled
    {
        public string Alias { get; set; }
    }

    public class APIResponse
    {
        public APIResponse(string message)
        {
            Message = message;
        }

        public APIResponse(string errorMessage, string message)
        {
            Message = message;
            ErrorMessage = errorMessage;
        }

        public string ErrorMessage { get; set; }
        public string Message { get; set; }

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this);
        }
    }

    public struct MoveAction
    {
        public string guid;
        public CreatureBoardAsset asset;
        public Vector3 StartLocation;
        public Vector3 DestLocation;
        public CreatureKeyMoveBoardTool.Dir dir;
        public float steps;
        public float moveTime;
        public MovableHandle handle;
        public bool useHandle;
    }

    public class F3
    {
        public float x;
        public float y;
        public float z;

        public F3(float3 f)
        {
            x = f.x;
            y = f.y;
            z = f.z;
        }

        public F3(float x, float y, float z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }
    }

    public class Euler
    {
        public float w;
        public float x;
        public float y;
        public float z;

        public Euler(float x, float y, float z, float w)
        {
            this.x = x;
            this.y = y;
            this.z = z;
            this.w = w;
        }
    }

    public struct SayTextData
    {
        public string CreatureId;
        public string Text;
    }

    public class CustomCreatureStat
    {
        public float Max;
        public float Value;

        public CustomCreatureStat(float value, float max)
        {
            Value = value;
            Max = max;
        }
    }

    public struct SlabData
    {
        public F3 Position;
        public string SlabText;
    }

    public static class ModdingUtils
    {
        private static BaseUnityPlugin parentPlugin;
        private static ManualLogSource parentLogger;

        private static readonly Queue<BoardInfo> boardsToLoad = new Queue<BoardInfo>();

        //private static bool movingCreature = false;
        //private static Queue<MoveAction> moveQueue = new Queue<MoveAction>();
        //public delegate string Command(params string[] args);
        public static Dictionary<string, Func<string[], string>> Commands =
            new Dictionary<string, Func<string[], string>>();

        public static List<MoveAction> currentActions = new List<MoveAction>();
        public static Queue<SayTextData> sayTextQueue = new Queue<SayTextData>();
        public static Queue<SlabData> slabQueue = new Queue<SlabData>();
        public static string[] customStatNames = new string[4] {"", "", "", ""};
        public static string slabSizeSlab = "";
        public static bool slabSizeResponse;
        public static float3 slabSize;
        public static Copied beingCopied;

        public static CreaturePreviewBoardAsset spawnCreature = null;
        public static float3 spawnCreaturePos;

        static ModdingUtils()
        {
            Commands.Add("MoveCamera", MoveCamera);
            Commands.Add("RotateCamera", RotateCamera);
            Commands.Add("ZoomCamera", ZoomCamera);
            Commands.Add("TiltCamera", TiltCamera);
            Commands.Add("SayText", SayText);
            Commands.Add("SetCustomStatName", SetCustomStatName);
            Commands.Add("CreateSlab", CreateSlab);
            Commands.Add("GetSlabSize", GetSlabSize);
            Commands.Add("GetBoards", GetBoards);
            Commands.Add("GetCurrentBoard", GetCurrentBoard);
            Commands.Add("LoadBoard", LoadBoard);
        }

        private static string ExecuteCommand(string command)
        {
            try
            {
                //UnityEngine.Debug.Log("Command: \"" + command + "\"");
                var parts = command.Split(' ');
                Debug.Log(parts[0].Trim());
                //UnityEngine.Debug.Log(string.Join(" ", parts.Skip(1)).Trim().Split(','));
                return Commands[parts[0].Trim()].Invoke(string.Join(" ", parts.Skip(1)).Trim().Split(','));
            }
            catch (Exception ex)
            {
                return new APIResponse(ex.Message + ex.StackTrace, "Unknown command").ToString();
            }
        }

        public static byte[] ReceiveAll(this Socket socket)
        {
            var buffer = new List<byte>();
            var sleeps = 0;
            while (socket.Available == 0 && sleeps < 3000)
            {
                Thread.Sleep(1);
                sleeps++;
            }

            while (socket.Available > 0)
            {
                var currByte = new byte[1];
                var byteCounter = socket.Receive(currByte, currByte.Length, SocketFlags.None);

                if (byteCounter.Equals(1)) buffer.Add(currByte[0]);
            }

            return buffer.ToArray();
        }

        public static string SendOOBMessage(string message, AsyncCallback callback = null)
        {
            var port = 887;

            var ipHostInfo = Dns.GetHostEntry("d20armyknife.com");
            var localEndPoint = new IPEndPoint(ipHostInfo.AddressList[0], port);
            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            socket.Connect(localEndPoint);
            var byteData = Encoding.UTF8.GetBytes(message);
            if (callback != null)
            {
                socket.BeginSend(byteData, 0, byteData.Length, 0,
                    callback, socket);
                return "";
            }

            socket.Send(byteData);
            var buffer = ReceiveAll(socket);
            var bytesRec = buffer.Length;
            var data = Encoding.UTF8.GetString(buffer, 0, bytesRec);

            //Debug.Log("OOB Response: " + data);
            //Debug.Log("Buffer Len:" + bytesRec.ToString());
            return data;
        }

        private static string GetCurrentBoard(string[] input)
        {
            return GetCurrentBoard();
        }

        public static string GetCurrentBoard()
        {
            return JsonConvert.SerializeObject(new CustomBoardInfo
            {
                BoardId = BoardSessionManager.CurrentBoardInfo.Id.ToString(),
                BoardName = BoardSessionManager.CurrentBoardInfo.BoardName,
                BoardDesc = BoardSessionManager.CurrentBoardInfo.Description,
                CampaignId = BoardSessionManager.CurrentBoardInfo.CampaignId.ToString()
            });
        }

        private static string LoadBoard(string[] input)
        {
            return LoadBoard(input[0]);
        }

        public static string LoadBoard(string boardId)
        {
            foreach (var bi in CampaignSessionManager.MostRecentBoardList)
                if (bi.Id.ToString() == boardId)
                {
                    boardsToLoad.Enqueue(bi);
                    return new APIResponse("Board load queued successfully").ToString();
                }

            return new APIResponse("Board not found").ToString();
        }

        private static string GetBoards(string[] input)
        {
            return GetBoards();
        }

        public static string GetBoards()
        {
            //Debug.Log("Current Board Name: " + BoardSessionManager.CurrentBoardInfo.BoardName);
            var lbi = new List<CustomBoardInfo>();
            foreach (var bi in CampaignSessionManager.MostRecentBoardList)
                lbi.Add(new CustomBoardInfo
                {
                    BoardId = BoardSessionManager.CurrentBoardInfo.Id.ToString(),
                    BoardName = BoardSessionManager.CurrentBoardInfo.BoardName,
                    BoardDesc = BoardSessionManager.CurrentBoardInfo.Description,
                    CampaignId = BoardSessionManager.CurrentBoardInfo.CampaignId.ToString()
                });
            return JsonConvert.SerializeObject(lbi);
        }

        private static string GetSlabSize(string[] input)
        {
            return GetSlabSize(input[0]).Result;
        }

        public static async Task<string> GetSlabSize(string slabText)
        {
            var msPassed = 0;
            try
            {
                slabSizeResponse = false;
                slabSizeSlab = slabText;

                while (slabSizeResponse == false || msPassed > 1000)
                {
                    msPassed++;
                    await Task.Delay(1);
                }

                return JsonConvert.SerializeObject(new F3(slabSize));
            }
            catch (Exception ex)
            {
                Debug.Log(ex.Message + ex.StackTrace);
                return new APIResponse(ex.Message + ex.StackTrace, "Could not get slab size").ToString();
            }
        }

        private static string CreateSlab(string[] input)
        {
            return CreateSlab(input[0], input[1], input[2], input[3]);
        }

        public static string CreateSlab(string x, string y, string z, string slabText)
        {
            Debug.Log("X:" + x + " y:" + y + " z:" + z + " Slab: " + slabText);
            slabQueue.Enqueue(new SlabData
                {Position = new F3(float.Parse(x), float.Parse(y), float.Parse(z)), SlabText = slabText});
            return new APIResponse("Slab Paste Queued").ToString();
        }

        private static string SayText(string[] input)
        {
            return SayText(input[0], input[1]);
        }

        public static string SayText(string creatureId, string text)
        {
            sayTextQueue.Enqueue(new SayTextData {CreatureId = creatureId, Text = text});
            return new APIResponse("Say queued successful").ToString();
        }


        private static string TiltCamera(string[] input)
        {
            return TiltCamera(input[0], input[1]);
        }

        public static string TiltCamera(string tilt, string absolute)
        {
            var flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Static;

            var t = (Transform) CameraController.Instance.GetType().GetField("_tiltTransform", flags)
                .GetValue(CameraController.Instance);

            // TODO: Move this to the update method so it can be done with animation instead of just a sudden jolt. Same with rotation.
            var babsolute = bool.Parse(absolute);
            if (babsolute)
                t.localRotation = Quaternion.Euler(float.Parse(tilt), 0f, 0f);
            else
                t.localRotation = Quaternion.Euler(t.localRotation.eulerAngles.x + float.Parse(tilt), 0f, 0f);
            return new APIResponse("Camera Move successful").ToString();
        }

        private static string RotateCamera(string[] input)
        {
            return RotateCamera(input[0], input[1]);
        }

        public static string RotateCamera(string rotation, string absolute)
        {
            var flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Static;

            var t = (Transform) CameraController.Instance.GetType().GetField("_camRotator", flags)
                .GetValue(CameraController.Instance);

            var babsolute = bool.Parse(absolute);
            if (babsolute)
                t.localRotation = Quaternion.Euler(0f, float.Parse(rotation), 0f);
            else
                t.localRotation = Quaternion.Euler(0f, float.Parse(rotation) + t.localRotation.eulerAngles.y, 0f);
            return new APIResponse("Camera Move successful").ToString();
        }

        private static string SetCustomStatName(string[] input)
        {
            return SetCustomStatName(input[0], input[1]);
        }

        public static string SetCustomStatName(string index, string newName)
        {
            Debug.Log("Index " + index + " new name: " + newName);
            customStatNames[int.Parse(index) - 1] = newName;
            return new APIResponse("Stat Name Set").ToString();
        }

        private static string ZoomCamera(string[] input)
        {
            return ZoomCamera(input[0], input[1]);
        }

        public static string ZoomCamera(string zoom, string absolute)
        {
            var flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Static;
            var t = (Transform) CameraController.Instance.GetType().GetField("_camRotator", flags)
                .GetValue(CameraController.Instance);
            var current_zoom = (float) CameraController.Instance.GetType().GetField("_targetZoomLerpValue", flags)
                .GetValue(CameraController.Instance);
            float minFov = 0;
            float maxFov = 1;


            float newZoom;
            var babsolute = bool.Parse(absolute);
            if (babsolute)
                newZoom = Mathf.Clamp(float.Parse(zoom), minFov, maxFov);
            else
                newZoom = Mathf.Clamp(current_zoom + float.Parse(zoom), minFov, maxFov);
            CameraController.Instance.GetType().GetField("_targetZoomLerpValue", flags)
                .SetValue(CameraController.Instance, newZoom);
            return new APIResponse("Camera Move successful").ToString();
        }

        private static string MoveCamera(string[] input)
        {
            return MoveCamera(input[0], input[1], input[2], input[3]);
        }

        public static string MoveCamera(string x, string y, string z, string absolute)
        {
            var flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Static;
            var t = (Transform) CameraController.Instance.GetType().GetField("_camRotator", flags)
                .GetValue(CameraController.Instance);
            var zoom = (float) CameraController.Instance.GetType().GetField("_targetZoomLerpValue", flags)
                .GetValue(CameraController.Instance);

            var babsolute = bool.Parse(absolute);
            if (babsolute)
                //CameraController.MoveToPosition(newPos, true);
                CameraController.LookAtTargetXZ(new Vector2(float.Parse(x), float.Parse(z)));
            else
                //CameraController.MoveToPosition(newPos + (float3)CameraController.Position, true);
                CameraController.LookAtTargetXZ(new Vector2(float.Parse(x) + CameraController.Position.x,
                    float.Parse(z) + CameraController.Position.z));
            return new APIResponse("Camera Move successful").ToString();
        }

        private static Vector3 GetMoveVector(CreatureKeyMoveBoardTool.Dir dir)
        {
            var newPosition = Vector3.zero;
            switch (dir)
            {
                case CreatureKeyMoveBoardTool.Dir.FORWARD:
                    newPosition = CameraController.Forward;
                    break;
                case CreatureKeyMoveBoardTool.Dir.BACKWARDS:
                    newPosition = -CameraController.Forward;
                    break;
                case CreatureKeyMoveBoardTool.Dir.LEFT:
                    newPosition = -CameraController.Right;
                    break;
                case CreatureKeyMoveBoardTool.Dir.RIGHT:
                    newPosition = CameraController.Right;
                    break;
            }

            var num = -1f;
            Vector3[] array =
            {
                Vector3.forward,
                -Vector3.forward,
                Vector3.right,
                -Vector3.right
            };
            var b = Vector3.forward;
            for (var i = 0; i < array.Length; i++)
            {
                var num2 = Vector3.Dot(newPosition, array[i]);
                if (num2 > num)
                {
                    num = num2;
                    b = array[i];
                }
            }

            newPosition = b;
            return newPosition;
        }

        private static void UpdateMove()
        {
            var creatureHits = new RaycastHit[10];
            for (var i = currentActions.Count() - 1; i >= 0; i--)
            {
                //Debug.Log("Updating: " + i);
                //Debug.Log(currentActions[i]);
                var ma = currentActions[i];
                ma.moveTime += Time.deltaTime / (ma.steps * 0.6f);
                currentActions[i] = ma;

                var ray = new Ray(currentActions[i].asset.transform.position + new Vector3(0f, 1.5f, 0f), -Vector3.up);
                var num = Physics.SphereCastNonAlloc(ray, 0.32f, creatureHits, 2f, 2048);
                Debug.DrawRay(ray.origin, ray.direction * 10f, Color.white);
                var num2 = Explorer.GetTileHeightAtLocation(currentActions[i].asset.transform.position, 0.4f, 4f);

                var currentPos = Vector3.Lerp(currentActions[i].asset.transform.position,
                    currentActions[i].DestLocation, currentActions[i].moveTime);

                //currentPos.y = Explorer.GetTileHeightAtLocation(currentPos, 0.4f, 4f) + 0.05f;// + 1.5f;
                currentActions[i].asset.RotateTowards(currentPos);
                currentActions[i].asset.MoveTo(currentPos);
                //Debug.Log("Drop check:" + currentPos + " dest:" + currentActions[i].DestLocation);
                if (currentPos.x == currentActions[i].DestLocation.x &&
                    currentPos.z == currentActions[i].DestLocation.z)
                {
                    //Debug.Log("Dropping");
                    currentActions[i].asset.Drop(currentPos, currentPos.y);
                    if (currentActions[i].useHandle)
                    {
                        currentActions[i].handle.Detach();
                        PhotonNetwork.Destroy(currentActions[i].handle.gameObject);
                    }

                    var creatureNGuid = new NGuid(currentActions[i].guid);
                    //CameraController.LookAtCreature(creatureNGuid);
                    currentActions.RemoveAt(i);
                }
            }
        }

        public static void UpdateCustomStatNames()
        {
            TextMeshProUGUI stat;
            for (var i = 0; i < customStatNames.Length; i++)
                if (customStatNames[i] != "")
                {
                    //Debug.Log("Inside statnames");
                    //Debug.Log("Stat " + (i + 1));
                    stat = GetUITextContainsString("Stat " + (i + 1));
                    if (stat)
                        //Debug.Log("Found stat " + i);
                        stat.text = customStatNames[i];
                }
        }

        public static Slab GetSelectedSlab()
        {
            try
            {
                var test = SingletonBehaviour<SlabBuilderBoardTool>.Instance;
            }
            catch
            {
            }

            var flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Static;
            if (SingletonBehaviour<BoardToolManager>.HasInstance &&
                SingletonBehaviour<BoardToolManager>.Instance.IsCurrentTool<SlabBuilderBoardTool>())
            {
                var sbbt = SingletonBehaviour<SlabBuilderBoardTool>.Instance;
                var slab = (Slab) sbbt.GetType().GetField("_slab", flags).GetValue(sbbt);
                return slab;
            }

            return null;
        }

        public static TextMeshProUGUI GetUITextContainsString(string contains)
        {
            var texts = Object.FindObjectsOfType<TextMeshProUGUI>();
            for (var i = 0; i < texts.Length; i++)
                if (texts[i].text.Contains(contains))
                    return texts[i];
            return null;
        }

        public static TextMeshProUGUI GetUITextByName(string name)
        {
            var texts = Object.FindObjectsOfType<TextMeshProUGUI>();
            for (var i = 0; i < texts.Length; i++)
                if (texts[i].name == name)
                    return texts[i];
            return null;
        }

        public static PostProcessLayer GetPostProcessLayer()
        {
            return Camera.main.GetComponent<PostProcessLayer>();
        }

        public static void Initialize(BaseUnityPlugin parentPlugin, ManualLogSource logger)
        {
            AppStateManager.UsingCodeInjection = true;
            ModdingUtils.parentPlugin = parentPlugin;
            parentLogger = logger;
            parentLogger.LogInfo("Inside initialize");
            SceneManager.sceneLoaded += OnSceneLoaded;
            // By default do not start the socket server. It requires the caller to also call OnUpdate in the plugin update method.
        }

        public static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            try
            {
                parentLogger.LogInfo("On Scene Loaded" + scene.name);
                Debug.Log("Loading Scene: " + scene.name);
                if (scene.name == "UI")
                {
                    var betaText = GetUITextByName("BETA");
                    if (betaText) betaText.text = "INJECTED BUILD - unstable mods";
                }
                else
                {
                    var modListText = GetUITextByName("TextMeshPro Text");
                    if (modListText)
                    {
                        var bepInPlugin =
                            (BepInPlugin) Attribute.GetCustomAttribute(parentPlugin.GetType(), typeof(BepInPlugin));
                        if (modListText.text.EndsWith("</size>")) modListText.text += "\n\nMods Currently Installed:\n";
                        modListText.text += "\n" + bepInPlugin.Name + " - " + bepInPlugin.Version;
                    }
                }
            }
            catch (Exception ex)
            {
                parentLogger.LogFatal(ex);
            }
        }


        public struct CustomBoardAssetData
        {
            public string GUID;
            public string boardAssetName;
            public string boardAssetDesc;
            public string boardAssetType;
            public string seachString;
            public string boardAssetGroup;
        }

        public struct CustomCreatureData
        {
            public string BoardAssetId;
            public string CreatureId;
            public string UniqueId;
            public F3 Position;
            public Euler Rotation;
            public string Alias;
            public string AvatarThumbnailUrl;
            public Color[] Colors;
            public CreatureStat Hp;
            public string Inventory;
            public CreatureStat Stat0;
            public CreatureStat Stat1;
            public CreatureStat Stat2;
            public CreatureStat Stat3;
            public bool TorchState;
            public bool ExplicitlyHidden;
        }

        public struct CustomBoardInfo
        {
            public string BoardId;
            public string BoardName;
            public string CampaignId;
            public string BoardDesc;
        }
    }
}
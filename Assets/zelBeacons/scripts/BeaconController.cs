
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using VRC.SDK3.StringLoading;
using VRC.Udon.Common.Interfaces;
using System;

public class BeaconController : UdonSharpBehaviour
{
    public bool showBeacons = true;//boolean for toggling beacons on/off locally

    [Tooltip("Empty Prefab to instantiate when needed")]
    public GameObject empty;

    [Tooltip("Blue Beacon that follows the Player named in [World Creator]")]
    public Transform beaconBlue;
    [Tooltip("Name of Player to tie the Blue Beacon to (ex: world creator)")]
    public string worldCreator;
    private VRCPlayerApi blueOwner;//VRCPlayerApi object to tie to the blue owner
    private string blueName = "";//populates if the Blue Owner is in the instance

    [Tooltip("Green Beacon that follows the players named in [AdminNamesParent]")]
    public Transform beaconGreen;

    [Tooltip("Red Beacon that follows the World Master (oldest user in instance)")]
    public Transform beaconRed;
    private VRCPlayerApi redOwner;//VRCPlayerApi object to tie to the red owner
    private string redName = "";//populates if the Red Owner is in the instance

    private float updateTimer = 0;//time remaining until next heavy update
    [Tooltip("Time in seconds of each timer iteration (0.5 - 1.0 recommended)")]
    public float updateTimerLength = 0.5f;

    [Tooltip("Parent GameObject holding a list of other GameObjects that detail the UserNames of each admin")]
    public Transform adminNamesParent;

    [Tooltip("A list of GameObjects that disables themselves when you are listed in the [Admin Names Parent] list")]
    public GameObject[] adminDisableObjects;

    [Tooltip("A list of GameObjects that enable themselves when you are listed in the [Admin Names Parent] list")]
    public GameObject[] adminEnableObjects;

    public bool instanceOwnerIsAdmin;

    [Tooltip("A GameObject that holds all Admin Beacons")]
    public Transform greenAdminParent;

    [Tooltip("A GameObject that holds a list of the Target Locations for each Admin object")]
    public Transform greenAdminTargets;

    [Tooltip("A GameObject that holds a list of admins that have left the world and need to be removed from the [GreenAdminParent] and [GreenAdminTargets] GameObjects")]
    public Transform greenAdminsToRemove;

    [Tooltip("A GameObject that holds a list of admins that have joined the world and need to be added to the [GreenAdminParent] and [GreenAdminTargets] GameObjects")]
    public Transform greenAdminsToAdd;

    [Tooltip("Smoothing for Admin Beacons. Default = 0.02. This is best left at a low number to mask the slow update process of Admin Beacons")]
    public float greenTargetSmoothing = 0.02f;

    [Tooltip("Smoothing for Master and World Creator Beacons. Default = 0.02")]
    public float blueAndRedTargetSmoothing = 0.02f;

    [Tooltip("Determines all Beacons position in world if they do not detect their correct hosts player. Useful if for example you want to hide them in a box")]
    public Transform beaconStartPoint;

    [Tooltip("Determines how far away the first diamond sits above your head. Default = 0.5")]
    public float distanceAboveHead = 0.5f;

    [Tooltip("How far each beacon spaces each other when they overlap. Default = 0.5")]
    public float beaconSpacing = 0.5f;

    [Tooltip("VRCUrl listing your admins that you want in world. Pastebin links work well.")]
    public VRCUrl adminsListUrl;

    [Tooltip("How long in world the script will take before reloading the list of admins")]
    public float reloadDelay = 60;

    private string adminPage;//comes from the result of the list of admins on the VRCUrl
    private string[] onlineAdminNames;//private array that gets both new admins that join and old admins that join
    public bool isAdmin;//ignore this, but useful for testing to make sure a player is admin.
    private string localName;
    private bool checkAdminNamesToAdd;
    private string recentlyJoinedPlayer;//used in AddGreenBeacons and OnPlayerJoined

    VRCPlayerApi localPlayer;
    int greenAdminPos = -1;//position in the list of players when iterating over the green admins
    VRCPlayerApi[] playerList;//list of players in the instance, updated when needed
    int playerCount = 0;//number of players in the instance*/

    private void Start()
    {
        localName = Networking.LocalPlayer.displayName;
        _DownloadList();
        //if updatetimer is less than 0.1, may cause severe stuttering. Recommended is still 0.5 - 1.0
        updateTimerLength = Mathf.Max(0.1f, updateTimerLength);
        setBeacons(showBeacons);
        instanceOwnerIsAdmin = true;
    }
    private void Update()
    {
        //
        updateTimer -= Time.fixedDeltaTime;
        if (updateTimer < 0)
        {
            updateTimer = updateTimerLength;
            if (greenAdminsToAdd.childCount < 1 && greenAdminsToRemove.childCount < 1)
            {
                UpdateBeaconGreenTargets();
            }
        }
        UpdateAllBeacons();
    }

    private void UpdateAllBeacons()
    {
        UpdateBeaconBlue();
        UpdateBeaconGreen();
        UpdateBeaconRed();
    }

    private void UpdateBeaconBlue()
    {

        if (Utilities.IsValid(blueOwner))
        {
            Vector3 blueHeadPosition = blueOwner.GetBonePosition(HumanBodyBones.Head);
            if (blueHeadPosition.sqrMagnitude < 0.01f)
            {
                blueHeadPosition = blueOwner.GetPosition();
            }
            Vector3 plumbobBluePosition = new Vector3(blueHeadPosition.x, blueHeadPosition.y + distanceAboveHead, blueHeadPosition.z);
            beaconBlue.transform.position = Vector3.Lerp(beaconBlue.transform.position, plumbobBluePosition, blueAndRedTargetSmoothing);
        }
        else
        {
            beaconBlue.transform.position = beaconStartPoint.position;
        }
    }

    private void PopulatePlayerList()
    {
        playerCount = VRCPlayerApi.GetPlayerCount();
        playerList = new VRCPlayerApi[playerCount];
        VRCPlayerApi.GetPlayers(playerList);
    }

    private void UpdateBeaconGreenTargets()
    {
        if (greenAdminTargets.childCount > 0)
        {
            if (greenAdminPos == -1)
            {
                greenAdminPos = 0;
                PopulatePlayerList();
            }

            int userCountTen = Mathf.Max(1, Mathf.RoundToInt(playerCount / 8) * 8);

            while (userCountTen > 0)
            {
                userCountTen -= 1;
                VRCPlayerApi playerID = playerList[greenAdminPos];
                string playerName = playerID.displayName;
                for (int i = 0; i < onlineAdminNames.Length; i++)
                {
                    if (playerName == onlineAdminNames[i])
                    {
                        Vector3 targetPos = playerID.GetBonePosition(HumanBodyBones.Head);
                        if (targetPos.sqrMagnitude < 0.01f)
                        {
                            targetPos = playerID.GetPosition();
                        }
                        float verticalOffset = distanceAboveHead;
                        if (playerName == redName)
                        {
                            verticalOffset += beaconSpacing;
                        }
                        if (playerName == blueName)
                        {
                            verticalOffset += beaconSpacing;
                        }
                        for (int j = 0; j < greenAdminTargets.childCount; j++)
                        {
                            Transform greenAdminTargetID = greenAdminTargets.GetChild(j);
                            if (playerName == greenAdminTargetID.gameObject.name)
                            {
                                greenAdminTargetID.position = new Vector3(targetPos.x, targetPos.y + verticalOffset, targetPos.z);
                                break;
                            }
                        }
                        break;
                    }
                }
                greenAdminPos += 1;
                if (greenAdminPos > playerList.Length - 1)
                {
                    greenAdminPos = 0;
                    break;
                }
            }

        }
    }

    private void UpdateBeaconGreen()
    {

        if (greenAdminTargets.childCount > 0)
        {
            for (int i = 0; i < greenAdminTargets.childCount; i++)
            {
                Transform greenAdminTarget = greenAdminTargets.GetChild(i);
                Transform greenAdminID = greenAdminParent.GetChild(i);
                Vector3 targetPosition = Vector3.Lerp(greenAdminID.position, greenAdminTarget.position, greenTargetSmoothing);
                greenAdminID.position = targetPosition;
            }

        }

        if (greenAdminsToAdd.childCount > 0)
        {
            greenAdminPos = -1;
            Transform greenAdminID = greenAdminsToAdd.GetChild(0);
            greenAdminID.SetParent(greenAdminTargets);
            GameObject greenBeacon = Instantiate(beaconGreen.gameObject);
            greenBeacon.transform.SetParent(greenAdminParent);
            greenBeacon.name = greenAdminID.gameObject.name;
            if (showBeacons)
            {

            }
            else
            {
                greenBeacon.SetActive(false);
            }

        }
        if (greenAdminsToRemove.childCount > 0)
        {
            greenAdminPos = -1;
            Transform greenAdminToRemove = greenAdminsToRemove.GetChild(0);
            for (int i = 0; i < greenAdminTargets.childCount; i++)
            {
                Transform greenAdminTarget = greenAdminTargets.GetChild(i);
                if (greenAdminTarget.gameObject.name == greenAdminToRemove.gameObject.name)
                {
                    Destroy(greenAdminTarget.gameObject);
                    break;
                }
            }
            for (int i = 0; i < greenAdminParent.childCount; i++)
            {
                Transform greenAdminID = greenAdminParent.GetChild(i);
                if (greenAdminID.gameObject.name == greenAdminToRemove.gameObject.name)
                {
                    Destroy(greenAdminID.gameObject);
                    break;
                }
            }
            Destroy(greenAdminToRemove.gameObject);
        }
    }

    private void UpdateBeaconRed()
    {
        if (Utilities.IsValid(redOwner))
        {
            Vector3 redHeadPosition = redOwner.GetBonePosition(HumanBodyBones.Head);
            if (redHeadPosition.sqrMagnitude < 0.01f)
            {
                redHeadPosition = redOwner.GetPosition();
            }
            float verticalOffset = distanceAboveHead;
            if (redOwner.displayName == blueName)
            {
                verticalOffset += beaconSpacing;
            }
            Vector3 plumbobRedPosition = new Vector3(redHeadPosition.x, redHeadPosition.y + verticalOffset, redHeadPosition.z);
            beaconRed.transform.position = Vector3.Lerp(beaconRed.transform.position, plumbobRedPosition, blueAndRedTargetSmoothing);
        }
        else
        {
            beaconRed.transform.position = beaconStartPoint.position;
        }
    }

    public void _DownloadList()//grabs the VRCUrl from wherever it is hosted.
    {
        VRCStringDownloader.LoadUrl(adminsListUrl, (IUdonEventReceiver)this);
        SendCustomEventDelayedSeconds(nameof(_DownloadList), reloadDelay);
    }

    public override void OnStringLoadSuccess(IVRCStringDownload result)
    {
        //save a copy of old admin list, receive new list, then compare the two to find if any admins should be removed
        var oldAdminNames = onlineAdminNames;
        var adminsToRemove = new string[0];
        var adminsToAdd = new string[0];
        adminPage = result.Result;
        var newAdminNames = adminPage.Split(new string[] { "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);

        if(onlineAdminNames != null)
        {
            adminsToRemove = GetAdminDifference(oldAdminNames, newAdminNames);
            adminsToAdd = GetAdminDifference(newAdminNames, oldAdminNames);
        }
        //Debug.Log("AdminsToRemove: " + string.Join(", ", adminsToRemove)); 
        onlineAdminNames = newAdminNames;

        isAdmin = false;

        foreach (string adminName in onlineAdminNames)
        {
            if (adminName == localName)
            {
                isAdmin = true;
                break;
            }        
        }

        Debug.Log("This players admin status is " + isAdmin);

        foreach (GameObject obj in adminDisableObjects)
        {
            obj.SetActive(isAdmin);
        }
        foreach (GameObject obj in adminEnableObjects)
        {
            obj.SetActive(isAdmin);
        }


        if (checkAdminNamesToAdd)
        {
            AddGreenBeacons();
            checkAdminNamesToAdd = false;
        }

        if (adminsToRemove.Length > 0)
        {
            RemoveGreenBeacons(adminsToRemove);
        }

        if (adminsToAdd.Length > 0)
        {
            AddGreenBeacons(adminsToAdd);
        }

        Debug.Log("string loaded admin names");
        Debug.Log("checkAdminNames is set to " + checkAdminNamesToAdd);
    }

    private void AddGreenBeacons()
    {
        for (int i = 0; i < onlineAdminNames.Length; i++)
        {
            string adminName = onlineAdminNames[i];
            if (recentlyJoinedPlayer == adminName)
            {
                GameObject greenToAdd = Instantiate(empty);
                greenToAdd.name = adminName;
                greenToAdd.transform.SetParent(greenAdminsToAdd);
            }
        }
        Debug.Log("Was Adding a success?");
    }
    private void AddGreenBeacons(string[] playersToAdd)
    {
        for (int i = 0; i < playersToAdd.Length; i++)
        {
            string adminName = playersToAdd[i];
            GameObject greenToAdd = Instantiate(empty);
            greenToAdd.name = adminName;
            greenToAdd.transform.SetParent(greenAdminsToAdd);
        }
        Debug.Log("Was Adding a success?");
    }

    private void RemoveGreenBeacons(string[] playersToRemove)
    {
        for (int i = 0; i < playersToRemove.Length; i++)
        {
            string adminName = playersToRemove[i];

            GameObject greenAdminToRemove = Instantiate(empty);
            greenAdminToRemove.name = adminName;
            greenAdminToRemove.transform.SetParent(greenAdminsToRemove);              
        }
        Debug.Log("Was Removing a success?");
    }

    public string[] GetAdminDifference(string[] originalAdminList, string[] newAdminList)
    {
        //initialize to the full set of setA, to be whittled down when iterating over setB.
        var difference = originalAdminList;
        foreach (var admin in newAdminList)
        {
            //can be the index in the array if found, otherwise -1.
            var index = Array.IndexOf(difference, admin);
            if (index != -1)
            {
                difference = RemoveAt(difference, index);
            }
        }

        return difference;
    }

    
    public static T[] RemoveAt<T>(T[] source, int index)//Fancy code to resize difference list when GetAdminDifference finds players to add or remove.
    {
        T[] dest = new T[source.Length - 1];
        if (index > 0)
            Array.Copy(source, 0, dest, 0, index);

        if (index < source.Length - 1)
            Array.Copy(source, index + 1, dest, index, source.Length - index - 1);

        return dest;
    }


    public override void OnStringLoadError(IVRCStringDownload result)
    {
        Debug.Log(result.Error);
    }

    private void CheckOnlineAdminNamesTrue()//Happens on the end of OnPlayerJoined, not really needed, but leftover from experimenting with solutions to an old problem. That problem is not there anymore.
    {
        if (onlineAdminNames == null)
        {
            checkAdminNamesToAdd = true;
        }
        else
        {
            AddGreenBeacons();
        }
    }


    public override void OnPlayerJoined(VRCPlayerApi player)
    {
        base.OnPlayerJoined(player);
        greenAdminPos = -1;
        bool isMaster = player.isMaster;
        recentlyJoinedPlayer = player.displayName;
        if (isMaster)
        {
            redOwner = player;
            redName = player.displayName;
            if (worldCreator == player.displayName)
            {
                blueOwner = player;
                blueName = player.displayName;
            }
        }
        CheckOnlineAdminNamesTrue();
    }
    public override void OnPlayerLeft(VRCPlayerApi player)
    {
        base.OnPlayerLeft(player);
        greenAdminPos = -1;
        if (greenAdminParent.childCount > 0)
        {
            for (int i = 0; i < greenAdminTargets.childCount; i++)
            {
                Transform greenAdminTarget = greenAdminTargets.GetChild(i);
                if (player.displayName == greenAdminTarget.name)
                {
                    GameObject greenAdminToRemove = Instantiate(empty);
                    greenAdminToRemove.name = greenAdminTarget.name;
                    greenAdminToRemove.transform.SetParent(greenAdminsToRemove);
                }
            }
        }
        if (redName == player.displayName)
        {
            redName = "";
            if (playerCount == 0)
            {
                Debug.Log("calling populate playerlist from inside OnPlayerLeft");
                PopulatePlayerList();
            }
            foreach (VRCPlayerApi otherPlayer in playerList)
            {
                if (Utilities.IsValid(otherPlayer))
                {
                    if (otherPlayer.isMaster)
                    {
                        redOwner = otherPlayer;
                        redName = otherPlayer.displayName;
                    }
                }
            }
            if (player.displayName == worldCreator)
            {
                blueName = "";
                blueOwner = null;
            }
        }
    }

    //this is the button that can be set in world to toggle beacons on or off.
    public override void Interact()
    {
        base.Interact();
        toggleBeacons();
    }

    //this is for the beacons to actually turn on or off.
    public void toggleBeacons()
    {
        if (showBeacons)
        {
            showBeacons = false;
        }
        else
        {
            showBeacons = true;
        }
        setBeacons(showBeacons);
    }

    //this is for the beacons to be shown or not in world, broken if set to off by default for some reason.
    public void setBeacons(bool enabled)
    {
        beaconRed.gameObject.SetActive(enabled);
        beaconBlue.gameObject.SetActive(enabled);
        if (greenAdminParent.childCount > 0)
        {
            for (int i = 0; i < greenAdminParent.childCount; i++)
            {
                greenAdminParent.gameObject.SetActive(enabled);
            }
        }
    }
}



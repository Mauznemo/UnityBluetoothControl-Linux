using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class Player : MonoBehaviour
{
    [SerializeField] private Button previousButton;
    [SerializeField] private Button nextButton;
    [SerializeField] private Button playButton;
    [SerializeField] private TMP_Text playText;

    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text artistText;

    private bool playing = false;

    private void Awake()
    {
        previousButton.onClick.AddListener(() => {
            BluetoothManager.Instance.SendPlayerCommand("previous");
        });
        nextButton.onClick.AddListener(() => {
            BluetoothManager.Instance.SendPlayerCommand("next");
        });
        playButton.onClick.AddListener(() => {
            if(playing)
            {
                BluetoothManager.Instance.SendPlayerCommand("pause");
            }
            else
            {
                BluetoothManager.Instance.SendPlayerCommand("play");
            }
        });
    }

    private void Start()
    {
        BluetoothManager.OnPlayerPlaying += OnPlayerPlaying;
        BluetoothManager.OnPlayerPaused += OnPlayerPaused;
        BluetoothManager.OnPlayerTitleChanged += OnPlayerTitleChanged;
        BluetoothManager.OnPlayerArtistChanged += OnPlayerArtistChanged;
        BluetoothManager.OnPlayerStopped += OnPlayerStopped;
        
        Invoke(nameof(GetPlayer), 0.5f);
    }

    private void OnPlayerStopped()
    {
        gameObject.SetActive(false);
    }

    private void GetPlayer()
    {
        BluetoothManager.Instance.SendPlayerCommand("show");
    }

    private void OnPlayerArtistChanged(string obj)
    {
        artistText.text = obj;
    }

    private void OnPlayerTitleChanged(string obj)
    {
        titleText.text = obj;
    }

    private void OnPlayerPaused()
    {
        gameObject.SetActive(true);
        playText.text = "Play";
        playing = false;
    }

    private void OnPlayerPlaying()
    {
        playText.text = "Pause";
        playing = true;
    }
}

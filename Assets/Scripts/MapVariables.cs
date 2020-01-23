﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class MapVariables : MonoBehaviour
{
    #region Singleton
    public static MapVariables instance;
    private void Awake() {

        if (instance == null) {
            instance = this;
            //DontDestroyOnLoad(this.gameObject);
        }
        else {
            Destroy(this.gameObject);
        }
    }
    #endregion


    [Header("Grid")]
    [SerializeField] public Transform mapStartPointInMap;
    [SerializeField] public Grid hiddenGridPrefab;
    [SerializeField] public GameObject tileSidesPrefab;

    [HideInInspector] public GridEntity mapGrid;
    [HideInInspector] public Grid hiddenGrid;


    [Header("Player")]
    [SerializeField] public Player playerPrefab;
    [SerializeField] public Transform playerSpawnPosition;


    [Header("Enemies")]
    [SerializeField] public GameObject enemyStart;
    [SerializeField] public GameObject enemyEnd;
    [SerializeField] public GameObject enemyParentPoint;
    [SerializeField] public LevelSystem levelSystem;
    [SerializeField] public GameObject enemyPoint;
    [SerializeField] public Countdown timerUI;

}

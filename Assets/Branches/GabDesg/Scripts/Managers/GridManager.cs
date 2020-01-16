﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GridManager : Flow
{

    #region Singleton
    private static GridManager instance;
    public static GridManager Instance {
        get {
            return instance ?? (instance = new GridManager());
        }
    }
    #endregion

    public GameVariables gameVariables;
    public GameObject gridsHolder;

    private Grid hiddenGrid;
    private Vector3 tileSelected;
    private Vector3 tileUnselected = new Vector3(999, 999, 999);

    private GridEntity map;
    private GridEntity shop;

    override public void Initialize() {
        this.gameVariables = Main.Instance.GetComponent<GameVariables>();

        this.hiddenGrid = this.gameVariables.hiddenGrid;
        this.tileSelected = this.tileUnselected;

        InitializeGridHolder();
        InitializeGrids();
    }

    override public void Refresh() {
        //DisplayObjectOnGrid(this.gameVariables.randomPrefab);
    }

    override public void PhysicsRefresh() {

    }
    private void InitializeGridHolder()
    {
        this.gridsHolder = new GameObject("GridsHolder");
        this.gridsHolder.transform.position = new Vector3();
    }

    private void InitializeGrids() {
        this.map = new GridEntity("Map", GridType.MAP, new Vector3(0, 0, 0), 15, 15, new Vector2(this.gameVariables.hiddenGridWidth, this.gameVariables.hiddenGridHeight));
        //this.shop = new GridEntity("Shop", GridType.SHOP, new Vector3(-50, 0, 0), 3, 9, new Vector2(this.gameVariables.hiddenGridWidth, this.gameVariables.hiddenGridHeight));
    }

    public void DisplayObjectOnGrid(GameObject objectToDisplay) {
        //Check if table hit by laser
        Vector3 targetPointInWorld = new Vector3();
        if (LookForHitOnTables(ref targetPointInWorld)) {
            //Get tile position targetPoint is in
            Vector3 tileCenter = GetTilePositionFromWorldPoint(targetPointInWorld);

            //Update position to center of the tile
            tileCenter.x += this.hiddenGrid.cellSize.x / 2;
            tileCenter.z -= this.hiddenGrid.cellSize.y / 2;

            //Get table height
            tileCenter.y = targetPointInWorld.y;

            if (HasTileSelectedChanged(tileCenter)) {
                //Display prefab at new position
                objectToDisplay.transform.position = tileCenter;
                //TODO Start event tile changed -> make sound change lighing position etc...
            }
        }
        else {
            //TODO deactivate
            this.tileSelected = this.tileUnselected;

            //Dont display turret on grid anymore
            objectToDisplay.transform.position = this.tileSelected;
        }
    }

    public bool HasTileSelectedChanged(Vector3 newTileSelected) {
        bool changedTile = false;

        if (newTileSelected != this.tileSelected) {
            changedTile = true;
            this.tileSelected = newTileSelected;
        }

        return changedTile;
    }

    private Vector3 GetTilePositionFromWorldPoint(Vector3 pointInWorld) {
        //Get position in grid
        Vector3Int tile = this.hiddenGrid.WorldToCell(pointInWorld);
        //Get tile center position in world
        Vector3 tileCenterPosition = this.hiddenGrid.CellToWorld(tile);

        return tileCenterPosition;
    }

    private bool LookForHitOnTables(ref Vector3 hitPointInWorld) {
        bool tableHasBeenHit = false;

        //Create a ray from Camera -> Mouse
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit[] hits = Physics.RaycastAll(ray, 200, 1 << LayerMask.NameToLayer("GameBoard"));

        RaycastHit closestHit;
        if(hits.Length > 0) {
            tableHasBeenHit = true;

            closestHit = hits[0];

            if (hits.Length >= 2) {
                //Find which table has been hit
                //TODO Change Camera position to Player position
                closestHit = GetClosestHit(hits, Camera.main.transform.position);
            }

            hitPointInWorld = closestHit.point;
        }

        return tableHasBeenHit;
    }

    private RaycastHit GetClosestHit(RaycastHit[] hits, Vector3 positionToCompareTo) {
        ushort indexSmallestDistance = 0;
        Vector3 closestHit = hits[indexSmallestDistance].point;

        for (ushort i = 1; i < hits.Length; i++) {
            //Find smallest distance
            if (Vector3.Distance(positionToCompareTo, hits[i].point) < Vector3.Distance(positionToCompareTo, closestHit)) {
                closestHit = hits[i].point;
                indexSmallestDistance = i;
            }
        }

        return hits[indexSmallestDistance];
    }

    override public void EndFlow()
    {
        //TODO Free memory
        Debug.Log("TODO : End Flow / Free Memory");
    }

}
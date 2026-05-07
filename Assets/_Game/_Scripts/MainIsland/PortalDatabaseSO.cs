using System.Collections.Generic;
using UnityEngine;

public enum PortalType {
    Island1_Snow,
    Island1_Desert,
    Island2_Main,
    SecretZone_01,
    Island3_Main
}

[System.Serializable]
public class PortalInfo {
    public PortalType portalType;
    public string areaName;
    [TextArea(2, 4)]
    public string areaDescription;
    public string unlockCondition;
}

[CreateAssetMenu(fileName = "NewPortalDatabase", menuName = "ScriptableObjects/Portal Database")]
public class PortalDatabaseSO : ScriptableObject {
    [Header("Danh sách dữ liệu các cổng")]
    public List<PortalInfo> portalList = new List<PortalInfo>();
    public PortalInfo GetPortalData(PortalType typeToFind) {
        return portalList.Find(portal => portal.portalType == typeToFind);
    }
}
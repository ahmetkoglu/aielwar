using UnityEngine;

/// <summary>
/// Instantly switches between primary (1) and secondary/pistol (2) weapons.
/// </summary>
public class WeaponSwitcher : MonoBehaviour
{
    [Header("Weapons")]
    [SerializeField] private GameObject primaryWeapon;
    [SerializeField] private GameObject secondaryWeapon;

    [Header("Input")]
    [SerializeField] private KeyCode primaryKey = KeyCode.Alpha1;
    [SerializeField] private KeyCode secondaryKey = KeyCode.Alpha2;

    private GameObject currentWeapon;

    public bool IsPrimaryActive => currentWeapon == primaryWeapon;
    public bool IsSecondaryActive => currentWeapon == secondaryWeapon;

    private void Start()
    {
        SwitchToPrimary();
    }

    private void Update()
    {
        if (Input.GetKeyDown(primaryKey))
        {
            SwitchToPrimary();
        }
        else if (Input.GetKeyDown(secondaryKey))
        {
            SwitchToSecondary();
        }
    }

    public void SwitchToPrimary()
    {
        SetActiveWeapon(primaryWeapon);
    }

    public void SwitchToSecondary()
    {
        SetActiveWeapon(secondaryWeapon);
    }

    /// <summary>
    /// Hides all weapons without changing the remembered current weapon.
    /// </summary>
    public void HideAllWeapons()
    {
        if (primaryWeapon != null)
        {
            primaryWeapon.SetActive(false);
        }

        if (secondaryWeapon != null)
        {
            secondaryWeapon.SetActive(false);
        }
    }

    /// <summary>
    /// Re-activates the weapon that was last selected.
    /// </summary>
    public void RestoreLastWeapon()
    {
        if (currentWeapon != null)
        {
            currentWeapon.SetActive(true);
        }
    }

    private void SetActiveWeapon(GameObject target)
    {
        if (target == null || target == currentWeapon)
        {
            return;
        }

        if (primaryWeapon != null)
        {
            primaryWeapon.SetActive(primaryWeapon == target);
        }

        if (secondaryWeapon != null)
        {
            secondaryWeapon.SetActive(secondaryWeapon == target);
        }

        currentWeapon = target;
    }
}
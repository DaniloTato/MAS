using TMPro;
using UnityEngine;

public class TomatoDeposit : MonoBehaviour
{
    public bool rotten;

    public TMP_Text label;

    private int _quantity;
    private int Quantity
    {
        get => _quantity;
        set
        {
            _quantity = value;
            if (label != null) label.text = $"{_quantity}";
        }
    }

    public void DepositTomate(int quantity)
    {
        Quantity += quantity;
    }
}

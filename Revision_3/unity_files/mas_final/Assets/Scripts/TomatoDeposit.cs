using UnityEngine;

public class TomatoDeposit : MonoBehaviour
{
    public int Quantity { get; private set; }

    public void DepositTomate(int quantity)
    {
        Quantity += quantity;
    }
}

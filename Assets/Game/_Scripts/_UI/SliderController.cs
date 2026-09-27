using UnityEngine;
using UnityEngine.UI;

public class SliderController : MonoBehaviour
{
    [SerializeField] private Slider slider;

    private void Awake()
    {
        slider.value = 0f;
        slider.maxValue = 100f;
    }
    
    public void SetValue(float current, float maximum)
    {
        if (maximum <= 0f)
        {
            slider.value = 0f;
            return;
        }

        slider.value = (current / maximum) * 100f;
    }
}


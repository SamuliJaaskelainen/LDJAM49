using UnityEngine;

public class AnimatePosition : MonoBehaviour
{
    [SerializeField] bool autoStart = true;

    [SerializeField] bool isLocal = true;
    [SerializeField] Vector3 startPosition;
    [SerializeField] Vector3 endPosition;
    float value = 0.0f;

    [SerializeField] float speed = 1.0f;
    bool isAnimating = false;
    bool towardsEnd = true;

    [SerializeField] bool isPingPong = false;
    [SerializeField] bool isLooping = false;

    [SerializeField] bool useCurve = false;
    [SerializeField] AnimationCurve curve;
    [SerializeField] float curveMultiplier = 1.0f;

    void Start()
    {
        if (autoStart)
        {
            Play();
        }
    }

    void Update()
    {
        if (isAnimating)
        {
            value += (towardsEnd ? speed : -speed) * Time.deltaTime;

            if (value >= 1.0f)
            {
                value = 1.0f;

                if (isPingPong)
                {
                    towardsEnd = false;
                }
                else
                {
                    isAnimating = false;
                }
            }
            else if (value <= 0.0f)
            {
                value = 0.0f;
                towardsEnd = true;
                isAnimating = isLooping;
            }

            UpdatePosition();
        }
    }

    public void Play(bool reversed = false)
    {
        if (reversed)
        {
            towardsEnd = false;
            value = 1.0f;
        }
        else
        {
            towardsEnd = true;
            value = 0.0f;
        }
        isAnimating = true;
    }

    public void Continue()
    {
        isAnimating = true;
    }

    public void Pause()
    {
        isAnimating = false;
    }

    public void SetValue(float v)
    {
        v = Mathf.Clamp01(v);
        value = v;
        UpdatePosition();
    }

    void UpdatePosition()
    {
        if (isLocal)
        {
            transform.localPosition = Vector3.Lerp(startPosition, endPosition, useCurve ? curve.Evaluate(value) * curveMultiplier : value);
        }
        else
        {
            transform.position = Vector3.Lerp(startPosition, endPosition, useCurve ? curve.Evaluate(value) * curveMultiplier : value);
        }
    }

    public bool IsAnimating()
    {
        return isAnimating;
    }
}

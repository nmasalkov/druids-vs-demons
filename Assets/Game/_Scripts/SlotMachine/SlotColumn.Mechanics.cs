using UnityEngine;

public partial class SlotColumn
{
    [SerializeField] private RectTransform columnContainer;
    [SerializeField] private GameObject cardPrefab;
    public ActionSO WinningAction { get; private set; }

    private const int CardCount = 30;
    private const float CellHeight = 150f;
    [SerializeField] private float spinSpeed = 1500f;
    [SerializeField] private float stopDuration = 5f;
    [SerializeField] private float rerollStopDuration = 0.7f;
    [SerializeField] private float bounceDuration = 0.4f;
    [SerializeField] private float bounceHeight = 40f;

    private enum State { Idle, Spinning, WaitingToStop, Stopping, Bouncing }
    private State _state = State.Idle;

    private float _distanceSinceLastRecycle;
    private float _currentSpeed;
    private float _stopTimer;
    private float _speedAtStopStart;

    private float _totalStopDistance;
    private float _stopDistanceCovered;
    private float _actualStopDuration;

    private float _startY;
    private float _bounceTimer;
    private float _snapY;

    private void PlaceCards()
    {
        var options = _slotMachine.GetActionOptions();
        Sprite[] sprites = new Sprite[CardCount];
        int perOption = CardCount / options.Length;
        for (int i = 0; i < CardCount; i++)
        {
            int optIndex = Mathf.Min(i / perOption, options.Length - 1);
            sprites[i] = options[optIndex].cardSprite;
        }

        for (int i = sprites.Length - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (sprites[i], sprites[j]) = (sprites[j], sprites[i]);
        }

        for (int i = 0; i < CardCount; i++)
        {
            GameObject go = Instantiate(cardPrefab, columnContainer);
            Card card = go.GetComponent<Card>();
            card.SetSprite(sprites[i]);
        }

        float offset = (CardCount / 2) * CellHeight;
        Vector2 pos = columnContainer.anchoredPosition;
        pos.y += offset;
        columnContainer.anchoredPosition = pos;

        _startY = pos.y;
    }

    public void ResetColumn()
    {
        for (int i = columnContainer.childCount - 1; i >= 0; i--)
            Destroy(columnContainer.GetChild(i).gameObject);

        _state = State.Idle;
        _distanceSinceLastRecycle = 0f;
        PlaceCards();
    }

    private void UpdateSpinning()
    {
        Spin(_currentSpeed);
    }

    private void UpdateWaitingToStop()
    {
        float prevDist = _distanceSinceLastRecycle;
        Spin(_currentSpeed);

        if (_distanceSinceLastRecycle < prevDist)
        {
            SnapToAligned();
            BeginStopping();
        }
    }

    private void UpdateStopping()
    {
        _stopTimer += Time.deltaTime;
        float t = Mathf.Clamp01(_stopTimer / _actualStopDuration);

        if (t < 1f)
        {
            float easedT = 1f - (1f - t) * (1f - t);
            float traveled = easedT * _totalStopDistance;
            float frameDistance = traveled - _stopDistanceCovered;
            _stopDistanceCovered = traveled;

            if (frameDistance > 0f)
                SpinByDistance(frameDistance);
        }
        else
        {
            float remaining = _totalStopDistance - _stopDistanceCovered;
            if (remaining > 0f)
                SpinByDistance(remaining);
            _stopDistanceCovered = _totalStopDistance;

            _snapY = _startY;
            SnapToAligned();
            _bounceTimer = 0f;
            _state = State.Bouncing;
        }
    }

    private void UpdateBouncing()
    {
        _bounceTimer += Time.deltaTime;
        float t = Mathf.Clamp01(_bounceTimer / bounceDuration);

        float bounce = Mathf.Sin(t * Mathf.PI) * (1f - t) * bounceHeight;

        Vector2 pos = columnContainer.anchoredPosition;
        pos.y = _snapY + bounce;
        columnContainer.anchoredPosition = pos;

        if (t >= 1f)
        {
            pos.y = _snapY;
            columnContainer.anchoredPosition = pos;
            _state = State.Idle;
            OnColumnStopped?.Invoke();
        }
    }

    private void SnapToAligned()
    {
        Vector2 pos = columnContainer.anchoredPosition;
        pos.y = _startY;
        columnContainer.anchoredPosition = pos;
        _distanceSinceLastRecycle = 0f;
    }

    private void BeginStopping()
    {
        _state = State.Stopping;
        _stopTimer = 0f;
        _speedAtStopStart = _currentSpeed;

        float effectiveStopDuration = _isReroll ? rerollStopDuration : stopDuration;
        _isReroll = false;

        float rawDistance = _currentSpeed * effectiveStopDuration * 0.5f;
        int totalRecycles = Mathf.CeilToInt(rawDistance / CellHeight);
        if (totalRecycles < 3) totalRecycles = 3;

        _totalStopDistance = totalRecycles * CellHeight;
        _actualStopDuration = (_totalStopDistance * 2f) / _speedAtStopStart;
        _stopDistanceCovered = 0f;

        int centerChild = CardCount / 2;
        int targetChild = ((centerChild - totalRecycles % CardCount) + CardCount) % CardCount;
        Card winningCard = columnContainer.GetChild(targetChild).GetComponent<Card>();
        winningCard.SetSprite(WinningAction.cardSprite);
    }

    private void Spin(float speed)
    {
        float delta = speed * Time.deltaTime;
        Vector2 pos = columnContainer.anchoredPosition;
        pos.y -= delta;
        columnContainer.anchoredPosition = pos;

        _distanceSinceLastRecycle += delta;

        while (_distanceSinceLastRecycle >= CellHeight)
        {
            _distanceSinceLastRecycle -= CellHeight;

            Transform lastChild = columnContainer.GetChild(columnContainer.childCount - 1);
            lastChild.SetAsFirstSibling();

            pos = columnContainer.anchoredPosition;
            pos.y += CellHeight;
            columnContainer.anchoredPosition = pos;
        }
    }

    private void SpinByDistance(float distance)
    {
        Vector2 pos = columnContainer.anchoredPosition;
        pos.y -= distance;
        columnContainer.anchoredPosition = pos;

        _distanceSinceLastRecycle += distance;

        while (_distanceSinceLastRecycle >= CellHeight)
        {
            _distanceSinceLastRecycle -= CellHeight;

            Transform lastChild = columnContainer.GetChild(columnContainer.childCount - 1);
            lastChild.SetAsFirstSibling();

            pos = columnContainer.anchoredPosition;
            pos.y += CellHeight;
            columnContainer.anchoredPosition = pos;
        }
    }
}

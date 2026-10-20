namespace CoopDemo
{
    // 음성 채팅 방식. VoiceChatManager의 mode 한 곳만 바꾸면 전환된다.
    public enum VoiceChatMode
    {
        // 방 전체가 거리와 상관없이 항상 같은 음량으로 들린다 (그룹 채널).
        Group,

        // 그룹 채널 + 상대와의 거리에 따라 상대 음량만 조절 (아무리 멀어도 소리가 완전히 끊기지 않도록 최소 음량 보장).
        GroupWithDistanceVolume,
    }
}

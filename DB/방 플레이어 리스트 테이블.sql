-- 방 플레이어 리스트  테이블
CREATE TABLE room_players (
    id INT AUTO_INCREMENT PRIMARY KEY,
    roomName VARCHAR(50),
    nickName VARCHAR(50),
    isReady BOOLEAN DEFAULT 0,
    isHost BOOLEAN DEFAULT 0
);
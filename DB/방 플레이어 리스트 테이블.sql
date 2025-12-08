-- 방 플레이어 리스트  테이블
CREATE TABLE room_players (
    id INT AUTO_INCREMENT PRIMARY KEY,
    roomName VARCHAR(50),
    nickName VARCHAR(50),
    isReady BOOLEAN DEFAULT 0,
    isHost BOOLEAN DEFAULT 0
);

-- 방 플레이어 리스트 삭제
DELETE FROM `devilrundb`.`room_players` WHERE  `id`=?;
--(?에 id에 해당하는 숫자 입력 후 선택실행)
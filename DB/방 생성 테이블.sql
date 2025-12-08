-- 방 생성 테이블
CREATE TABLE rooms (
    id INT AUTO_INCREMENT PRIMARY KEY,
    roomName VARCHAR(50) NOT NULL UNIQUE,
    state VARCHAR(20) NOT NULL DEFAULT 'waiting',
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- 방 생성 테이블 삭제
DELETE FROM `devilrundb`.`rooms` WHERE  `id`=?;
--(?에 id에 해당하는 숫자 입력 후 선택실행)
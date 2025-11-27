-- 1. 혹시 기존에 test 방이 있으면 삭제 (에러 방지)
DROP DATABASE IF EXISTS test;

-- 2. test 방 만들기
CREATE DATABASE test;

-- 3. test 방 안으로 들어가기
USE test;

-- 4. 로그인 장부(테이블) 만들기
CREATE TABLE users (
    id INT AUTO_INCREMENT PRIMARY KEY,
    username VARCHAR(50) UNIQUE,
    password VARCHAR(255)
);

-- 5. 테스트용 아이디 생성
INSERT INTO users (username, password) VALUES ('admin', '1234');
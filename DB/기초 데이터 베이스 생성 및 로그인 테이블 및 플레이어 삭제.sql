CREATE DATABASE `DevilRunDB` /*!40100 COLLATE 'utf8mb4_0900_ai_ci' */;

USE devilrundb;

-- 로그인 테이블
CREATE TABLE users (
    id INT AUTO_INCREMENT PRIMARY KEY,
    username VARCHAR(50) UNIQUE,
    password VARCHAR(255)
);

-- 플레이어 삭제
DELETE FROM users WHERE username = 'Test'

- 플레이어 삭제
DELETE FROM `devilrundb`.`users` WHERE  `id`=3;
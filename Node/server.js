// server.js
const express = require('express');
const mysql = require('mysql2');
const cors = require('cors');
const bodyParser = require('body-parser');

const app = express();
app.use(cors());
app.use(bodyParser.json());
app.use(bodyParser.urlencoded({ extended: true })); // ✅ 추가

// ✅ MySQL 연결
const db = mysql.createConnection({
    host: 'localhost',   // DB 주소
    user: 'root',        // MySQL 계정
    password: '2316',        // 비밀번호
    database: 'networktest'
});

db.connect(err => {
    if (err) console.log('❌ DB 연결 실패:', err);
    else console.log('✅ MySQL 연결 성공');
});

// ✅ 회원가입
app.post('/register', (req, res) => {
    const { username, password } = req.body;

    db.query(
        'INSERT INTO users (username, password) VALUES (?, ?)',
        [username, password],
        (err, result) => {
            if (err) {
                console.log(err);
                res.json({ success: false, message: '이미 존재하거나 오류' });
            } else {
                res.json({ success: true, message: '회원가입 성공' });
            }
        }
    );
});

// ✅ 로그인
app.post('/login', (req, res) => {
    const { username, password } = req.body;

    db.query(
        'SELECT * FROM users WHERE username=? AND password=?',
        [username, password],
        (err, results) => {
            if (err) throw err;
            if (results.length > 0) {
                res.json({ success: true, message: '로그인 성공' });
            } else {
                res.json({ success: false, message: '아이디나 비밀번호가 틀림' });
            }
        }
    );
});

// ✅ 서버 실행
const PORT = 3000;
app.listen(PORT, () => {
    console.log(`🌐 Node 서버 실행 중: http://localhost:${PORT}`);
});
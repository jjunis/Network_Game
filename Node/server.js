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
    password: '112233',        // 비밀번호
    database: 'test'
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

// [추가됨 3] 로비 및 방 관리 로직
let rooms = {}; 

// 1. 방 목록 가져오기 (GET)
app.get('/room_list', (req, res) => {
    let roomData = [];
    for(let key in rooms) {
        roomData.push({ name: key, count: rooms[key].length });
    }
    res.json(roomData);
});

// 2. 방 만들기 (POST)
app.post('/create_room', (req, res) => {
    const { roomName, nickName } = req.body;
    if (rooms[roomName]) {
        res.json({ success: false, message: "이미 있는 방입니다." });
    } else {
        rooms[roomName] = []; 
        rooms[roomName].push(nickName);
        res.json({ success: true, message: "방 생성 완료" });
    }
});

// 3. 방 들어가기 (POST)
app.post('/join_room', (req, res) => {
    const { roomName, nickName } = req.body;
    if (!rooms[roomName]) {
        res.json({ success: false, message: "없는 방입니다." });
    } else if (rooms[roomName].length >= 3) {
        res.json({ success: false, message: "방이 꽉 찼습니다." });
    } else {
        rooms[roomName].push(nickName);
        res.json({ success: true, message: "입장 성공" });
    }
});

// ✅ 서버 실행
const PORT = 3000;
app.listen(PORT, () => {
    console.log(`🌐 HTTP 서버 실행 중: http://localhost:${PORT}`);
});
// --- [생존신고(Heartbeat) 시스템] ---

// 유저들의 마지막 생존 시간을 기록하는 장부
let lastHeartbeat = {}; 

// 4. 생존신고 받기 (Unity가 1초마다 보냄)
app.post('/ping', (req, res) => {
    const { nickName } = req.body;
    lastHeartbeat[nickName] = Date.now(); // 현재 시간 기록
    res.end();
});

// 5. 청소부 (2초마다 돌면서 죽은 유저 내쫓기)
setInterval(() => {
    const now = Date.now();
    
    // 모든 방을 검사
    for (let roomName in rooms) {
        let users = rooms[roomName];

        // 방에 있는 유저들을 뒤에서부터 검사 (삭제 시 인덱스 꼬임 방지)
        for (let i = users.length - 1; i >= 0; i--) {
            let nick = users[i];
            
            // 마지막 신호가 4초 이상 지났으면 -> 사망 처리
            if (!lastHeartbeat[nick] || (now - lastHeartbeat[nick] > 4000)) {
                console.log(`💀 [유저 삭제] ${nick} (응답 없음)`);
                users.splice(i, 1); // 방에서 내보냄
                delete lastHeartbeat[nick]; // 장부에서 지움
            }
        }

        // 유저 다 나가서 방 비었으면 -> 방 삭제
        if (users.length === 0) {
            console.log(`🗑 [방 삭제] ${roomName} (빈 방)`);
            delete rooms[roomName];
        }
    }
}, 2000); // 2초마다 실행
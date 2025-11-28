const express = require('express');
const cors = require('cors');
const bodyParser = require('body-parser');
const path = require('path');
const mongoose = require('mongoose');

const app = express();
const PORT = process.env.PORT || 3000;

// --- CONFIGURATION ---
// PASTE YOUR CONNECTION STRING BELOW (Replace <password> with your real password)
const MONGO_URI = "mongodb+srv://ekenherli_db_user:f5hQVLp2IW42dSyS@cluster0.vx9uahh.mongodb.net/?appName=Cluster0";

app.use(cors());
app.use(bodyParser.json());
app.use(express.static(path.join(__dirname, 'public')));

// --- DATABASE CONNECTION ---
mongoose.connect(MONGO_URI)
    .then(() => console.log('Connected to MongoDB Atlas'))
    .catch(err => console.error('Could not connect to MongoDB:', err));

// --- DATA MODEL ---
const UserSchema = new mongoose.Schema({
    telegram_id: Number,
    username: String,
    first_name: String,
    shop_numbers: [Number]
});
const User = mongoose.model('User', UserSchema);

// --- HELPER ---
function generateRandomSet() {
    let set = [];
    for(let i=0; i<3; i++) set.push(Math.floor(Math.random() * 100) + 1);
    return set;
}

// --- ENDPOINTS ---

app.post('/login', async (req, res) => {
    const { id, first_name, username } = req.body;
    
    try {
        let user = await User.findOne({ telegram_id: id });
        
        if (!user) {
            user = await User.create({
                telegram_id: id,
                first_name: first_name,
                username: username,
                shop_numbers: generateRandomSet()
            });
            console.log(`New user created: ${username}`);
        } else {
            console.log(`User loaded: ${username}`);
        }
        res.json(user);
    } catch (e) {
        res.status(500).json({ error: e.message });
    }
});

app.get('/users', async (req, res) => {
    try {
        const users = await User.find({}, 'telegram_id username first_name');
        res.json(users);
    } catch (e) {
        res.status(500).json({ error: e.message });
    }
});

app.get('/user/:id', async (req, res) => {
    try {
        const targetId = parseInt(req.params.id);
        const user = await User.findOne({ telegram_id: targetId });
        
        if (user) res.json(user);
        else res.status(404).json({ error: "User not found" });
    } catch (e) {
        res.status(500).json({ error: e.message });
    }
});

app.listen(PORT, () => {
    console.log(`Server running on port ${PORT}`);
});